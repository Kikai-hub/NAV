using System;
using System.IO;
using NAV.Core.Save;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Video capture (editor only): plays a queue of clips staged by VideoCapture in Play Mode and
    /// records each with Unity Recorder into the video project. Every clip is a new throwaway
    /// world; its save is deleted afterwards, so captures never touch the developer's saves.
    /// Queue: clips separated by "||", e.g. NavVideoRecorder.Start("id=wolf;hour=10;spawn=ForestWolf:1:12||id=sea;...").
    /// Open Assets/Scenes/WorldGenTest.unity first. Keys: ProjectTitle/VIDEO_CAPTURE.md.
    /// </summary>
    [InitializeOnLoad]
    public static class NavVideoRecorder
    {
        public const string OutDir = "F:/Dev/RebonkVideo/public/footage/nav";

        private const string CompilePref = "ScriptCompilationDuringPlay";

        private static RecorderController _controller;
        private static double _waitUntil;

        static NavVideoRecorder()
        {
            EditorApplication.update += Tick;
        }

        public static void Start(string queue)
        {
            Directory.CreateDirectory(OutDir);
            // A recompile in the middle of a clip wipes every static; script changes wait until
            // Play ends. The developer's own setting comes back when the queue finishes.
            if (EditorPrefs.GetInt("navcap.active", 0) != 1)
            {
                EditorPrefs.SetInt("navcap.compilePref", EditorPrefs.GetInt(CompilePref, 0));
            }

            EditorPrefs.SetInt(CompilePref, 1);
            EditorPrefs.SetString("navcap.queue", queue);
            EditorPrefs.SetString("navcap.log", string.Empty);
            EditorPrefs.SetInt("navcap.active", 1);
        }

        public static void Abort()
        {
            EditorPrefs.SetString("navcap.queue", string.Empty);
            EditorPrefs.SetInt("navcap.done", 1);
            EditorPrefs.SetInt("navcap.rec", 0);
            EditorPrefs.SetFloat("navcap.clipStarted", -100000f);
        }

        public static string LogText => EditorPrefs.GetString("navcap.log", string.Empty);

        private static void Tick()
        {
            if (EditorPrefs.GetInt("navcap.active", 0) != 1
                || (!EditorApplication.isPlaying && (EditorApplication.isCompiling || EditorApplication.isUpdating))
                || EditorApplication.timeSinceStartup < _waitUntil)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                TickPlaying();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            MoveRecordings();
            DeleteCaptureSave();

            string queue = EditorPrefs.GetString("navcap.queue", string.Empty);
            if (queue.Length == 0)
            {
                Finish();
                return;
            }

            string[] clips = queue.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            EditorPrefs.SetString("navcap.queue", string.Join("||", clips, 1, clips.Length - 1));
            EditorPrefs.SetString("navcap.clip", clips[0]);
            EditorPrefs.SetInt("navcap.enabled", 1);
            EditorPrefs.SetInt("navcap.rec", 0);
            EditorPrefs.SetInt("navcap.recording", 0);
            EditorPrefs.SetInt("navcap.done", 0);
            EditorPrefs.SetFloat("navcap.clipStarted", (float)EditorApplication.timeSinceStartup);
            Log("start " + clips[0]);
            EditorApplication.isPlaying = true;
            _waitUntil = EditorApplication.timeSinceStartup + 3;
        }

        private static void TickPlaying()
        {
            bool wantRecording = EditorPrefs.GetInt("navcap.rec", 0) == 1;
            if (wantRecording && _controller == null)
            {
                BeginRecording();
            }
            else if (!wantRecording && _controller != null)
            {
                StopRecording();
                Log("recorded " + Value("id", "clip"));
            }

            // A clip that never reaches its end (an exception in the staging) is dropped, not
            // waited on forever. Offline recording of 4K can run well below real time.
            double limit = 240d + ParseFloat(Value("recLen", "8")) * 30d;
            if (EditorApplication.timeSinceStartup - EditorPrefs.GetFloat("navcap.clipStarted", 0f) > limit
                && EditorPrefs.GetInt("navcap.done", 0) != 1)
            {
                Log("TIMEOUT " + Value("id", "clip"));
                StopRecording();
                EditorPrefs.SetInt("navcap.done", 1);
            }

            if (EditorPrefs.GetInt("navcap.done", 0) == 1 && _controller == null)
            {
                EditorApplication.isPlaying = false;
                _waitUntil = EditorApplication.timeSinceStartup + 2;
            }
        }

        private static void BeginRecording()
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "NAV clip";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                TargetBitRate = 45f
            };
            movie.CaptureAudio = Value("audio", "1") != "0";
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = (int)ParseFloat(Value("width", "3840")),
                OutputHeight = (int)ParseFloat(Value("height", "2160"))
            };
            // Recorder mangles absolute paths outside the project; write to <project>/Recordings
            // (git-ignored) and move afterwards.
            movie.OutputFile = "Recordings/" + Value("id", "clip");
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60f;
            settings.CapFrameRate = true;

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            _controller.StartRecording();
            EditorPrefs.SetInt("navcap.recording", 1); // VideoCapture starts the clip clock on this
        }

        private static void StopRecording()
        {
            if (_controller != null && _controller.IsRecording())
            {
                _controller.StopRecording();
            }

            _controller = null;
            EditorPrefs.SetInt("navcap.recording", 0);
        }

        private static void MoveRecordings()
        {
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings");
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory, "*.mp4"))
            {
                string target = Path.Combine(OutDir, Path.GetFileName(file));
                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(file, target);
            }
        }

        private static void DeleteCaptureSave()
        {
            string saveId = EditorPrefs.GetString("navcap.saveId", string.Empty);
            if (saveId.Length > 0)
            {
                SaveSystem.DeleteSave(saveId);
            }

            EditorPrefs.SetString("navcap.saveId", string.Empty);
        }

        private static void Finish()
        {
            EditorPrefs.SetInt("navcap.active", 0);
            EditorPrefs.SetInt("navcap.enabled", 0);
            EditorPrefs.SetInt(CompilePref, EditorPrefs.GetInt("navcap.compilePref", 0));
            Log("DONE");
        }

        private static string Value(string key, string fallback)
        {
            foreach (string pair in EditorPrefs.GetString("navcap.clip", string.Empty).Split(';'))
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return parts[1].Trim();
                }
            }

            return fallback;
        }

        private static float ParseFloat(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        private static void Log(string line)
        {
            EditorPrefs.SetString("navcap.log", LogText + line + "\n");
            Debug.Log($"{nameof(NavVideoRecorder)}: {line}");
        }
    }
}

# NAV — дизайн HUD и меню: описания и промты

Документ для генерации примеров интерфейса (ChatGPT / любой генератор картинок) и
для последующей отрисовки ассетов. Пояснения на русском, промты на английском:
генераторы изображений точнее понимают английский. Промты можно копировать как есть.

Версия: 2026-10-22. Описывает то, что есть в игре сейчас (см. раздел 3), плюс
рекомендации, как это должно выглядеть.

------------------------------------------------------------------------

## 1. Общее направление

**Тон:** сказочная Древняя Русь. Уютно и таинственно, не мрачно (из GDD: «fairy-tale
Rus, not grimdark horror»). Интерфейс должен ощущаться как вещь из этого мира:
резное дерево, берёста, кованое железо, вышивка, а не как современный «плоский» UI
и не как фэнтези-золото в духе MMO.

**Источники мотивов (брать мотивы, не копировать):**
- деревянная резьба изб: наличники, причелины, «полотенца», геометрическая и
  растительная резьба;
- вышивка на рушниках: красная нить по льну, ромбы, «берегини», солярные знаки;
- берёзовая кора (береста): светлая, с тёмными чёрточками, грамоты на бересте;
- кованые железные накладки на сундуки и двери: заклёпки, полосы, петли;
- старинные рукописи: буквицы, киноварь (красные заглавные), устав/полуустав.

**Чего избегать:**
- фотореализма и сложных текстур: игра low-poly и стилизованная, интерфейс тоже
  должен быть простым и плоско-объёмным;
- перегруза орнаментом: орнамент только по краям рамок и на заголовках, внутри
  панелей спокойный фон;
- «скандинавского» вида (руны, драккары) и прямого копирования Valheim: у NAV
  должна быть своя, русская идентичность;
- кириллицы-«стилизации» под латиницу (псевдо-кириллица в английском тексте).

**Читаемость важнее декора:** игра в движении, HUD читается краем глаза. Контраст
текста к фону не ниже 4.5:1, полосы здоровья и выносливости различимы и без цвета
(по форме и положению).

------------------------------------------------------------------------

## 2. Палитра и шрифты

Текущая палитра игры (уже используется в меню, паузе, настройках) — её стоит
сохранить и развить:

| Роль | Цвет | Где |
|---|---|---|
| Тёмное дерево (фон панелей) | `#2B2118` (43, 33, 24) | панели |
| Дерево светлее (кнопки) | `#3D3023` (61, 48, 35) | кнопки, слоты |
| Наведение | `#503F2D` (80, 63, 45) | hover |
| Золото-охра (рамки, акценты) | `#8B6914` (139, 105, 20) | рамки |
| Светлое золото (активное) | `#C8A03C` (200, 160, 60) | выделение, бегунки |
| Кремовый текст | `#E6D2A0` (230, 210, 160) | основной текст |
| Приглушённый текст | `#968769` (150, 135, 105) | подсказки |

Добавить для HUD:

| Роль | Цвет | Где |
|---|---|---|
| Здоровье | `#B3261E` киноварь → `#7A1712` | полоса здоровья |
| Выносливость | `#D9A93A` охра → `#9C7420` | полоса выносливости |
| Еда (бафф) | `#6E8B3D` травяной | иконки еды |
| Опасность / снос | `#C0392B` | прицел при сносе |
| Береста (светлые панели, подсказки) | `#EFE6D2` | всплывающие подсказки |

**Шрифты** (бесплатные, с кириллицей, на случай локализации):
- заголовки: стилизация под старорусский устав — например **Ruslan Display** или
  **Kurale** (Google Fonts);
- основной текст и цифры: читаемый с засечками — **Old Standard TT**, **PT Serif**
  или **Lora**;
- цифры на HUD: моноширинные или табличные, чтобы не «прыгали».

------------------------------------------------------------------------

## 3. Что есть в игре сейчас (для точных промтов)

**HUD (во время игры, экран 16:9):**
- **Хотбар** — 6 квадратных слотов в левом верхнем углу, в слотах цифры 1–6 и
  иконки предметов; выбранный слот подсвечивается.
- **Здоровье** — вертикальная красная полоса в левом нижнем углу.
- **Выносливость** — горизонтальная жёлтая полоса снизу по центру.
- **Еда** — до трёх иконок съеденной еды с таймером (рядом с полосой здоровья).
- **Время и погода** — в правом верхнем углу: «День 1 · 08:00 · Ясно».
- **Прицел** — кольцо с точкой в центре; меняет цвет: нейтральный / цель для удара /
  красный при сносе постройки.
- **Подсказка взаимодействия** — под прицелом: «[E] Подобрать: Дерево».

**Окна:**
- Главное меню: название «NAV», подзаголовок, кнопки «Играть», «Загрузить»,
  «Настройки», «Выход»; экраны «Создание мира» (имя, сид, «Случайный») и
  «Загрузка» (список миров с датой и кнопкой удаления), диалог подтверждения.
- Пауза: «Продолжить», «Настройки», «Выйти в главное меню».
- Настройки: вкладки «Звук», «Управление», «Графика»; ползунки, переключатели,
  выпадающие списки, кнопки переназначения клавиш; «Сбросить», «Назад».
- Инвентарь: сетка 6 колонок × 4–5 рядов, вес «3,5/400 кг».
- Крафт: список рецептов слева, справа карточка (иконка, описание, характеристики,
  требование верстака, ингредиенты, кнопка «Создать»).
- Меню строительства: сетка деталей (фундамент, стена, крыша, дверь, частокол,
  верстак, костёр, сундук) и молот «Снос», строка с ценой.
- Контейнер: сундук / надгробие — сетка слотов рядом с инвентарём, кнопка
  «Забрать всё».
- Смерть: «Вы погибли», кнопка «Возродиться».
- Экран загрузки: надпись и полоса прогресса.

------------------------------------------------------------------------

## 4. Общий «префикс стиля» для всех промтов

Подставляйте его в начало любого промта ниже — так все картинки будут в одном стиле:

```
Game UI concept for "NAV", a stylized low-poly survival game set in fairy-tale
Ancient Rus (Slavic folklore). UI style: carved dark wood panels with thin
ochre-gold trim, subtle Slavic embroidery ornaments (red thread rhombus and solar
motifs) only on frame corners and headers, forged iron rivets and corner plates,
birch-bark accents for tooltips. Warm palette: dark wood #2B2118, ochre gold
#8B6914, cream text #E6D2A0, cinnabar red #B3261E. Old Russian ustav-style
display font for titles, clean readable serif for body text. Cozy, magical,
mysterious, not grimdark. Clean, readable, game-ready, flat-shaded with soft
bevels, no photorealism, no clutter, no Norse runes.
```

------------------------------------------------------------------------

## 5. Промты по экранам

### 5.1 HUD во время игры (главный пример)

**Описание:** камера от третьего лица, персонаж в лесу из елей, утро. HUD тонкий и
не перекрывает обзор: всё по краям, центр свободен, кроме прицела.

```
[STYLE PREFIX] In-game HUD mockup, 16:9 screenshot of a third-person low-poly game:
a hero in a simple linen shirt stands in a spruce forest at morning with soft fog.
HUD elements, all small and at the screen edges:
- top-left: hotbar of 6 square slots framed in carved dark wood with iron corner
  rivets, small numbers 1-6, item icons (stone axe, wooden hammer, wood logs,
  flint, raw meat, empty), the selected slot glows with a gold rim;
- bottom-left: vertical health bar shaped like a carved wooden staff with a
  cinnabar-red fill and an embroidered rhombus cap, next to it three small round
  food icons (cooked meat, berries, bread) with thin circular timers;
- bottom-center: thin horizontal stamina bar in ochre-gold inside a narrow
  wooden frame, fades when full;
- top-right: small birch-bark plate with "Day 1 · 08:00" and a tiny sun-and-cloud
  weather icon;
- center: minimal crosshair, a thin ring with a dot;
- just under the crosshair: interaction prompt "[E] Pick up: Wood" on a soft dark
  translucent strip.
Readable at a glance, lots of free space in the middle of the screen.
```

**Варианты для сравнения** (добавить в конец промта):
- `Variant: minimal — almost no ornaments, only thin gold lines.`
- `Variant: rich — more carving and embroidery on frames, still readable.`

### 5.2 HUD в бою и при низком здоровье

```
[STYLE PREFIX] Same HUD during combat at dusk: a tiger-like beast attacks the hero.
Health bar at 25% pulsing red with a faint red vignette at the screen edges;
stamina bar half empty; crosshair ring turned warm red because an enemy is
targeted; small damage indicator arc on the left side of the crosshair.
```

### 5.3 HUD в режиме строительства

```
[STYLE PREFIX] HUD in building mode: a translucent green ghost of a log wall snaps
to a wooden foundation; a dashed golden circle on the ground shows the workbench
build radius; under the crosshair a hint "Needs a workbench nearby" in an ochre
tone; bottom-center a small panel "Log Wall — 4 Wood" with the rotate key "R".
Hotbar shows the wooden hammer selected.
```

### 5.4 Главное меню

**Описание:** фон — живая сцена (поляна, изба, костёр, ели, сумерки или
рассвет), меню слева или по центру. Название крупно, в духе буквицы.

```
[STYLE PREFIX] Main menu screen. Background: a cozy low-poly clearing at dawn —
a log cabin with carved window frames, a campfire, spruce and birch trees,
light fog, a distant onion-domed wooden church silhouette. Left side: large title
"NAV" in an ornate Old Russian initial-letter style (red and gold, like an
illuminated manuscript), subtitle "A Fairy-Tale Rus Survival" below. Vertical
list of buttons as carved wooden plaques with iron rivets: Play, Load, Settings,
Quit; the hovered button has a warm gold glow and an embroidered underline.
Version text small in the bottom-right corner.
```

### 5.5 Создание мира и загрузка

```
[STYLE PREFIX] "Create World" panel: a parchment/birch-bark sheet pinned inside a
carved wooden frame. Fields: "World Name" text field, "Seed" field with a
"Randomize" button showing a small dice icon, big "Create World" button, smaller
"Back". Same screen style as the main menu, background blurred.
```

```
[STYLE PREFIX] "Load Game" panel: scrollable list of saved worlds, each row a
wooden plank with the world name, seed, date "21.10.2026 18:40", and a small iron
"✕" delete button on the right; a warning tag "older world version" on one row in
ochre; an empty-state text "No saves yet" variant. Confirm dialog on top:
"Delete 'Novgorod'? This cannot be undone." with Cancel and a red Delete button.
```

### 5.6 Пауза

```
[STYLE PREFIX] Pause menu over a darkened, slightly blurred game frame. Centered
small carved wooden panel with title "Paused" and three buttons: Resume,
Settings, Leave to Main Menu. Subtle embroidered ornament line under the title.
```

### 5.7 Настройки

```
[STYLE PREFIX] Settings window, centered large panel with three tabs styled as
wooden tabs with carved edges: Audio, Controls, Graphics (Controls active). Rows:
"Mouse Sensitivity" slider with a gold bead handle and value "1.00x", "Invert
Vertical Look" toggle as a small iron latch, then a key-binding list: "Move
Forward — W", "Jump — Space", "Attack — Left Button" shown as key caps carved
from wood; one row in listening state "Press a key..." glowing. Footer: "Reset to
Defaults" and "Back" buttons. Also show a small inset of the Graphics tab with
dropdowns (Preset: High, Shadows: Medium) styled as carved selectors.
```

### 5.8 Инвентарь и контейнер

```
[STYLE PREFIX] Inventory screen: left panel "Inventory" with a 6×4 grid of square
slots (dark wood cells, iron corners), item icons with stack counts (Wood 25,
Flint 7, Resin 3, stone axe, wooden hammer, raw meat 2), weight "3.5 / 400 kg"
with a small scale icon. Right panel "Chest" with a 5×2 grid and a "Take All"
button. One item is being dragged with a soft shadow; hovering shows a birch-bark
tooltip: item name, short description, weight, "Right-click to use".
```

### 5.9 Крафт

```
[STYLE PREFIX] Crafting window: left column a list of recipes as small wooden
tabs with icons (Stone Axe, Stone Pickaxe, Wooden Hammer, Stone Sword, Cooked
Meat); right side a recipe card on parchment: big icon, name "Stone Axe", short
lore line, stats (Damage 9, Tier 1), requirement "Workbench nearby" with a check
mark, ingredients with have/need counts in green/red ("Wood 3/3", "Flint 2/4"),
and a large "Craft" button disabled because not enough flint.
```

### 5.10 Меню строительства

```
[STYLE PREFIX] Build menu opened with the hammer: a radial or grid selector of
building pieces with small isometric icons — Foundation, Log Wall, Roof, Door,
Palisade, Workbench, Campfire, Chest — and a hammer icon for "Demolish". The
selected piece is highlighted; info line at the bottom "Log Wall — 4 Wood (12) —
Needs a workbench nearby". Carved wood style, compact, not covering the center.
```

### 5.11 Смерть и загрузка

```
[STYLE PREFIX] Death screen: the game frame desaturated with a dark red
vignette; centered title "You Died" in the Old Russian display font; a small
grave-mound icon with a wooden cross-post marker and text "Your belongings remain
at your grave"; button "Respawn".
```

```
[STYLE PREFIX] Loading screen: full-screen illustration of a misty spruce forest
with a wooden road sign; bottom a thin progress bar like a woven belt filling with
red-and-gold embroidery pattern; a short folklore tip text above it, e.g. "Spruce
trees fall away from your axe — stand aside."
```

------------------------------------------------------------------------

## 6. Промты для набора ассетов (когда стиль выбран)

Когда понравится один из макетов, по нему лучше сгенерировать отдельные элементы
на прозрачном фоне — их проще перенести в Unity:

```
[STYLE PREFIX] UI asset sheet on a transparent background, orthographic,
evenly spaced, no text: carved wooden panel frame (9-slice friendly, corners
separate from straight edges), square inventory slot (normal, hovered, selected),
wide button (normal, hover, pressed, disabled), tab (active, inactive), slider
track and bead handle, toggle latch (on/off), dropdown arrow, scrollbar,
vertical health bar frame, horizontal stamina bar frame, round food buff frame,
birch-bark tooltip background, crosshair variants (neutral, target, demolish).
```

```
[STYLE PREFIX] Set of 32 item icons, 256x256 each, transparent background,
3/4 view, soft outline, same lighting: wood logs, stone, flint, resin, raw meat,
cooked meat, stone axe, stone pickaxe, wooden hammer, stone sword, iron sword,
round wooden shield, hide, bone, fang, berries, bread, torch, chest, campfire.
```

------------------------------------------------------------------------

## 7. Технические требования (чтобы макет можно было собрать в Unity)

- Интерфейс собран на **UI Toolkit** (UXML/USS): рамки и кнопки лучше делать как
  **9-slice** (углы отдельно от растягиваемых краёв), без сплошных картинок на всё
  окно.
- Целевое разрешение макетов: **1920×1080**; элементы должны масштабироваться до
  1280×720 и 2560×1440.
- HUD: хотбар-слот ~56–64 px, полоса выносливости ~240×10 px, полоса здоровья
  ~16×150 px, шрифт подсказок не меньше 16 px.
- Иконки предметов: квадрат, прозрачный фон, единый ракурс и свет (сейчас иконки
  рендерятся из 3D-моделей меню NAV/Items/Render Icons — новый стиль иконок должен
  с ними сочетаться или заменить их целиком).
- Состояния для каждой кнопки: обычная, наведение, нажата, недоступна; для слота:
  пустой, с предметом, выбран, перетаскивание.
- Тексты в макетах лучше на английском (как сейчас в игре); если планируется
  русская локализация — проверить, что выбранные шрифты содержат кириллицу.

------------------------------------------------------------------------

## 8. Как пользоваться

1. Сгенерировать 5.1 (HUD) в двух-трёх вариантах (minimal / rich), выбрать стиль.
2. Тем же стилем — главное меню (5.4) и инвентарь (5.8): это самые заметные экраны.
3. Прислать выбранные картинки — по ним я обновлю USS (цвета, отступы, формы), а
   ассеты (раздел 6) можно будет нарезать и подключить как 9-slice фоны.

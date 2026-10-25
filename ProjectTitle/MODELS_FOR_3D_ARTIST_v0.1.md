# NAV — техническое задание на 3D-модели (v0.1, 2026-10-25)

Документ для 3D-художника. Здесь перечислены **все** модели игры, которые сейчас
стоят **заглушками** (собраны из примитивов — кубов, цилиндров, сфер — или
сгенерированы кодом) или взяты из **сторонних ассетов** (паков из Asset Store).
Задача — заменить их своими моделями в едином стиле игры.

Для каждой модели указано:

- **Сейчас** — чем она сейчас представлена в проекте (путь к префабу/ассету);
- **Что это в игре** — зачем нужна;
- **Размер и привязка** — габариты в метрах и где должна быть точка опоры (pivot);
- **Описание** — как должна выглядеть;
- **Промт** — готовый текст для ИИ-генератора (на английском: генераторы 3D понимают
  его лучше). К каждому промту добавлять **общий стилевой блок** из раздела 0.3.

Приоритет: **P1** — заглушка, заменить в первую очередь; **P2** — сторонний ассет,
заменить для единства стиля; **P3** — можно оставить надолго (декор, мелочь).

---

## 0. Общие требования

### 0.1 Стиль

- Сеттинг: **сказочная Древняя Русь** (не мрачный реализм, не «викинги»). Уютно,
  приключенчески, немного таинственно. Ориентиры: русские народные сказки, лубок,
  иллюстрации Билибина, деревянное зодчество (срубы, резные наличники, причелины,
  «солнышки»), Valheim по атмосфере.
- Форма: **стилизованный low-poly** — чистые крупные формы, читаемый силуэт,
  чуть утрированные пропорции, фаски на краях. Не фотореализм, не пиксель.
- Цвет: тёплое дерево (сосна, дуб), мох, лён, медь/бронза, тёмное железо, красная
  охра в росписях. Текстура — **простая рисованная** (hand-painted) или
  **цветовая палитра-атлас** (один атлас на группу моделей), без шума и грязи.
- Освещение в игре — динамическое (день/ночь, погода), поэтому **не запекать
  свет и тени** в текстуру (максимум лёгкий AO).

### 0.2 Технические требования (Unity 6, URP)

| Параметр | Требование |
|---|---|
| Формат | `.fbx` (Y вверх, Z вперёд, масштаб 1 юнит = 1 м, Apply Transform) |
| Масштаб | Рост персонажа **1.8 м**. Все размеры ниже — в метрах |
| Pivot | Как указано у каждой модели (обычно — **по центру основания**) |
| Полигоны | Мелкий предмет 300–1500 tri; оружие/инструмент 500–2500; постройка 1–5 тыс.; станция 2–6 тыс.; дерево 3–10 тыс. (LOD0); существо 5–15 тыс.; персонаж 10–25 тыс. |
| LOD | Деревья, камни, постройки — LOD0 + LOD1 (~50%) + LOD2 (~20%). Мелочи — без LOD |
| Текстуры | Albedo (+ Normal по желанию). 512–1024 px для предметов, 1024–2048 для построек/существ. Предпочтительно общий атлас на набор |
| Материал | URP/Lit. Листва/трава — Alpha Clip (вырез по альфе), двусторонние |
| Коллайдеры | Не нужны (делаются в Unity), но форма должна давать понятный простой коллайдер |
| Анимации | Существа и персонаж — Humanoid (персонаж) / Generic (звери), клипы отдельно, см. разделы |
| Именование | `NAV_<Категория>_<Название>` (например `NAV_Build_WoodWall`) |

Важно для строительства: стены, полы, крыша **стыкуются по сетке 2 м** —
размеры этих деталей должны совпадать точно (см. раздел 2).

### 0.3 Общий стилевой блок для промтов

Добавлять в конец каждого промта:

```
Style: stylized low-poly game asset, fairy-tale Ancient Rus (old Slavic folk tale),
hand-painted texture, warm natural colours, clean readable silhouette, slightly
exaggerated proportions, soft bevels, no baked lighting, no text, no logos,
single object on neutral background, game-ready topology, PBR albedo.
```

---

## 1. Персонаж и существа

### 1.1 Игрок — `Player` (P2)
- **Сейчас:** манекен из пака **Kevin Iglesias / Human Character Dummy** (`HumanCharacterDummy_M.fbx`, есть и `_F`) — безликая красная фигура. `Assets/NAV/Prefabs/Player/HumanDummy_F Red.prefab`, в сцене — мужская.
- **Что это в игре:** главный герой (вид от третьего лица). Носит броню (шлем, грудь, ноги — см. раздел 4), держит в правой руке инструмент/оружие, в левой — щит.
- **Размер и привязка:** рост 1.8 м, pivot между стопами на земле, лицом по +Z. **Humanoid-риг Unity** (Mecanim), совместимый с анимациями Kevin Iglesias (Basic Motions / Melee) и Brawler — анимации уже в проекте, переделывать их не нужно (полный список — раздел 9.1). Кость для предмета в правой руке — `B-handProp.R` (как у манекена), в левой — `B-handProp.L`.
- **Описание:** молодой славянский путник: льняная рубаха-косоворотка с вышивкой по вороту, порты, онучи с лаптями или поршнями, кожаный пояс с ножнами и мешочком. Две версии — мужская (русые волосы, короткая борода) и женская (коса). Нейтральная базовая одежда — поверх неё надевается броня.
- **Промт:**
```
Young Slavic traveller hero, full body, T-pose, linen kosovorotka shirt with red
embroidered collar, woven belt with pouch and knife sheath, wool trousers, leg
wraps (onuchi) and bast shoes (lapti), short light-brown hair and short beard,
friendly brave face, humanoid proportions 1.8 m. Female variant: long braid,
same clothing cut. Rig-ready, arms straight out, separate fingers.
```

### 1.2 Олень — `Deer` (P2)
- **Сейчас:** ассет **ithappy / Animals_FREE** (`Deer_001.fbx`). `Assets/NAV/Prefabs/Creatures/Deer.prefab`.
- **Что это в игре:** мирная дичь равнин и лесов, убегает от игрока; даёт шкуру, кость, мясо.
- **Размер и привязка:** ~1.5 м в длину, 1.1 м в холке (с рогами ~2.3 м), pivot на земле между ногами, морда по +Z. Generic-риг.
- **Анимации:** сейчас в паке только idle / walk / run — **нужны новые**: idle, graze (пасётся), walk, run, hit, death (подробно — раздел 9.2).
- **Описание:** благородный олень с ветвистыми рогами, тёплый рыже-бурый мех, светлое брюхо, немного «сказочный» — большие глаза, мягкие формы.
- **Промт:**
```
Red deer stag, quadruped, large branching antlers, warm reddish-brown fur with a
pale belly and white rump patch, big gentle eyes, slender legs, standing pose,
1.5 m long. Fairy-tale forest animal. Generic quadruped rig: idle, grazing, walk,
run, hit, death.
```

### 1.3 Лесной волк — `ForestWolf` (P1 — сейчас на модели тигра!)
- **Сейчас:** временно **модель тигра** из ithappy / Animals_FREE (`Tiger_001.fbx`) — заглушка. `Assets/NAV/Prefabs/Creatures/ForestWolf.prefab`.
- **Что это в игре:** главный хищник лесов, ходит стаями, ночью опаснее; укус 12 урона; даёт шкуру и клыки.
- **Размер и привязка:** ~1.5–1.7 м в длину, 0.8 м в холке, pivot на земле, морда по +Z. Generic-риг.
- **Анимации:** у тигра-заглушки только idle / walk / run — **нужен полный набор**: idle, walk, run, attack (укус), hit, death, howl (подробно — раздел 9.2).
- **Описание:** крупный серый лесной волк, густой мех с тёмной «седловиной», светлое брюхо, жёлтые глаза; слегка сказочно-зловещий, но не монстр.
- **Промт:**
```
Large grey forest wolf, quadruped, thick shaggy fur with a dark saddle on the back
and a pale chest, amber eyes, slightly menacing fairy-tale look, 1.6 m long,
0.8 m at the shoulder. Generic quadruped rig: idle, walk, run, bite attack, hit,
death, howl.
```

### 1.4 Надгробие игрока — `Gravestone` (P1)
- **Сейчас:** примитивы. `Assets/NAV/Prefabs/Player/Gravestone.prefab`.
- **Что это в игре:** появляется на месте смерти игрока, в нём лежат его вещи (можно открыть).
- **Размер и привязка:** ~1.0 × 1.5 × 0.25 м, pivot по центру основания.
- **Описание:** деревянный могильный крест-«голубец» (крест с двускатной крышечкой) или резной столбик с узором, у подножия холмик земли и котомка.
- **Промт:**
```
Old Slavic wooden grave marker "golubets": a carved wooden post-cross with a small
gabled roof on top, folk ornament carvings, small earth mound at the base with a
travel bundle leaning on it, 1.5 m tall.
```

---

## 2. Строительство (ставит игрок)

Все детали стыкуются по сетке: **плита фундамента 2×2 м, стена 2 м шириной и 2 м
высотой**. Срубы — из брёвен ~0.4 м в диаметре (5 венцов на стену), концы брёвен
выступают на ~0.18 м за угол («рубка в обло»). Сейчас все детали — **заглушки из
примитивов** (P1), сделанные кодом в `Editor/RusBuildingStyleSetup.cs` /
`OakSetup.cs`.

### 2.1 Базовый деревянный набор (сосна)

#### Фундамент — `WoodFoundation` (P1)
- **Сейчас:** примитивы: доски пола, окладной венец, камни по углам. `Assets/NAV/Prefabs/Build/WoodFoundation.prefab`.
- **Размер и привязка:** плита **2 × 0.2 × 2 м**; pivot в центре плиты (верх пола на +0.1 м). Под плитой — лежни и угловые камни (до −0.5 м, уходят в землю).
- **Описание:** дощатый пол на нижнем венце (лежни из брёвен), по углам — валуны-подкладки.
- **Промт:**
```
Building piece: square wooden floor platform 2 x 2 m, thick pine planks laid on a
log sill frame (two round logs along each edge, ends crossing at the corners),
flat field stones under each corner, top surface flat. Old Russian log-house base.
```

#### Стена-сруб — `WoodWall` (P1)
- **Сейчас:** 5 цилиндров-брёвен + мох в пазах. `WoodWall.prefab`.
- **Размер и привязка:** **2 × 2 × 0.2 м** (брёвна чуть толще визуально, до 0.32 м); pivot в **центре стены** (низ на −1 м). Концы брёвен выступают на 0.18 м с каждой стороны.
- **Описание:** 5 венцов круглых сосновых брёвен, между ними тёмный мох (конопатка), на торцах — видны годовые кольца. Две стены под углом должны красиво перекрещиваться торцами.
- **Промт:**
```
Building piece: log wall section 2 m wide, 2 m tall, five stacked round pine logs
(0.4 m thick) with dark moss caulking in the seams, log ends sticking out 0.18 m on
both sides with visible tree rings, Russian izba log cabin wall.
```

#### Дверь — `WoodDoor` (P1)
- **Сейчас:** брёвна по бокам проёма, резной наличник, дощатая створка с жиковинами (кованые петли) и кольцом. `WoodDoor.prefab`.
- **Размер и привязка:** как стена **2 × 2 × 0.2 м**, pivot в центре. **Проём 1.2 × 1.9 м** (персонаж 1.8 м должен проходить!). **Створка — отдельный объект** с осью вращения на левом краю проёма (x = −0.6 м от центра, низ на −1 м), открывается на ~100°.
- **Описание:** сруб с проёмом, над ним брус-перемычка; спереди резной наличник с «солнышками»; дверь из вертикальных досок на кованых жиковинах, кольцо-ручка.
- **Промт:**
```
Building piece: log wall 2 x 2 m with a door opening 1.2 m wide and 1.9 m tall,
carved wooden door frame (nalichnik) with sun-rosette ornaments, separate door leaf
of vertical planks with forged iron strap hinges and a ring handle, hinge on the
left edge. Old Russian izba entrance.
```

#### Скат крыши — `WoodRoof` (P1)
- **Сейчас:** доски-тёс в два слоя, слеги, резной подзор. `WoodRoof.prefab`.
- **Размер и привязка:** ширина **2 м**; скат поднимается на **2 м на 2 м** (45°) к коньку и **свисает на 0.5 м** за стену. Pivot — **на линии верха стены** (центр ширины): от pivot скат идёт вверх к −Z (конёк в точке y=+2, z=−2) и вниз к +Z (свес). Два ската над избой 4 м сходятся в конёк.
- **Описание:** тёсовая кровля — доски вдоль ската в два слоя внахлёст, снизу на свесе — резной подзор (доска с зубчиками).
- **Промт:**
```
Building piece: one roof slope 2 m wide, 45 degrees, rising 2 m over 2 m, made of
overlapping wooden shingle boards (tes) running down the slope in two layers, eave
overhanging 0.5 m with a carved decorative valance board with triangle teeth.
Old Russian wooden roof.
```

#### Фронтон — `WoodGable` (P1)
- **Сейчас:** процедурная сетка брёвен (`Art/Models/GableLogs.asset`). `WoodGable.prefab`.
- **Размер и привязка:** прямоугольный треугольник **2 м по основанию × 2 м высотой**, толщина ~0.3 м. Pivot — **центр основания**; высокий край слева (−X), к правому (+X) сходит на нет. Брёвна срезаны под 45° вровень с крышей.
- **Описание:** треугольный фронтон из горизонтальных брёвен, концы срезаны по скату; опционально — маленькое слуховое окошко на одном из двух.
- **Промт:**
```
Building piece: right-triangle log gable, base 2 m, height 2 m, horizontal round
logs with moss caulking, log ends cut at 45 degrees along the roof line, flat back.
Old Russian izba gable.
```

#### Частокол — `WoodPalisade` (P1)
- **Сейчас:** цилиндр + конус. `WoodPalisade.prefab`.
- **Размер и привязка:** бревно Ø ~0.26 м, высота **2 м**, pivot в центре (низ на −1 м).
- **Описание:** заострённое бревно частокола, кора местами снята, верх затёсан.
- **Промт:**
```
Single palisade stake: upright pine log 2 m tall, 0.26 m thick, sharpened pointed
top with axe-cut facets, bark partly stripped. Slavic hillfort palisade.
```

### 2.2 Дубовый набор (тир 2, столярный верстак)

Тёмное морёное дерево, богатая резьба. Все — **заглушки из примитивов** (P1),
`Assets/NAV/Prefabs/Build/Oak*.prefab`.

| Модель | Размер / привязка | Описание | Промт (+ стилевой блок) |
|---|---|---|---|
| `OakWall` — дубовая стена | 2 × 2 × 0.4 м, pivot в центре | сруб из толстых дубовых брёвен, тёмное дерево | `Heavy oak log wall 2 x 2 m, five thick dark oak logs, protruding carved ends` |
| `OakWindowWall` — стена с окном | 2 × 2 × 0.46 м | сруб с волоковым окном и резным наличником с ставнями | `Oak log wall 2 x 2 m with a small window, ornate carved window frame (nalichnik) with folk patterns and wooden shutters` |
| `OakPost` — столб | 0.36 × 2 × 0.36 м, pivot в центре | резной столб, «бочка» посередине, капитель | `Carved oak pillar 2 m tall, Russian terem style, bulging middle, carved capital` |
| `OakBeam` — балка | 2 × 0.26 × 0.26 м | брус-перекрытие | `Square hewn oak beam 2 m long, adze marks` |
| `OakFloor` — перекрытие | 2 × 0.2 × 2 м, pivot в центре | пол второго этажа на балках | `Oak plank floor panel 2 x 2 m on two beams, for a second storey` |
| `OakStairs` — лестница | 2 × 2 × 2 м (подъём на 2 м) | лестница с перилами-балясинами | `Wooden staircase rising 2 m over 2 m, oak treads, carved baluster railing` |
| `OakGate` — ворота | 4.6 × 2.9 × 0.3 м, **две створки отдельно** (петли по краям, открываются на 95°) | двустворчатые ворота с двускатной кровелькой и резьбой | `Double wooden gate 4 m wide, two leaves on iron hinges, small gabled roof on top with carved boards, Russian estate gate` |
| `OakRidge` — резной конёк | 2.4 × 0.7 × 0.2 м | охлупень/причелина с головой коня на торце | `Carved roof ridge ornament, horse head finial (okhlupen), folk carving, 2.4 m` |

### 2.3 Станции крафта (P1, все — примитивы)

#### Верстак — `WoodWorkbench`
- **Размер:** 1.6 × 1.1 × 0.8 м, pivot в центре основания.
- **Описание:** массивный стол на козлах, тиски, на стене-доске — топор, пила, долото, стружка.
- **Промт:** `Rustic carpenter's workbench 1.6 m long, thick plank top on trestle legs, wooden vice, hand tools (axe, saw, chisel) hanging on a back board, wood shavings`

#### Столярный верстак — `JoinersBench` (пристройка к верстаку)
- **Размер:** 2.0 × 1.0 × 0.7 м.
- **Описание:** длинный стол столяра с рубанками, коловоротом, заготовками резьбы.
- **Промт:** `Long joiner's bench 2 m, oak, with planes, brace drill, carved wooden ornament blanks, clamps`

#### Сундук — `WoodChest`
- **Размер:** 1.0 × 0.65 × 0.65 м, крышка — отдельный объект (петли сзади).
- **Описание:** окованный сундук с росписью (красные цветы по зелёному/охре).
- **Промт:** `Old Russian wooden chest, iron-bound corners and bands, hasp lock, painted floral folk pattern, separate lid`

#### Костёр — `Campfire`
- **Размер:** ~1.3 × 0.8 × 1.3 м; pivot в центре.
- **Описание:** кольцо камней, поленья шалашиком, над ним рогатины с вертелом (на вертеле готовится мясо). Огонь — отдельно (эффект в Unity).
- **Промт:** `Campfire: ring of stones, logs stacked in a teepee, two forked sticks with a wooden spit across the fire, no flames`

#### Кузня (горн) — `Forge`
- **Размер:** 1.9 × 3.1 × 1.2 м.
- **Описание:** каменный горн с дымоходом, мехи сбоку, кадка с водой, клещи.
- **Промт:** `Medieval Slavic forge hearth, stone and clay furnace with chimney 3 m tall, glowing coal pit, tongs, water barrel, no flames`

#### Наковальня — `Anvil` (пристройка к кузне)
- **Размер:** 0.55 × 0.95 × 0.95 м.
- **Промт:** `Blacksmith anvil on a thick oak stump, with hammer and tongs resting on it`

#### Мехи — `Bellows` (пристройка к кузне)
- **Размер:** 0.7 × 0.95 × 1.4 м.
- **Промт:** `Large leather blacksmith bellows on a wooden frame with a lever handle`

#### Литейная форма — `CastingMould` (пристройка, бронза)
- **Размер:** 1.3 × 0.75 × 0.8 м.
- **Промт:** `Bronze casting station: clay crucible, stone moulds for axe heads and ingots, sand box, small ladle`

#### Углежогная яма / печь — `CharcoalKiln`
- **Размер:** 2.4 × 1.1 × 2.4 м.
- **Описание:** куча брёвен, обложенная дёрном и землёй, дымит.
- **Промт:** `Charcoal clamp kiln: dome of stacked logs covered with turf and earth, 2.4 m wide, small vent holes, smoke hole on top`

#### Сыродутная печь (домница) — `Bloomery`
- **Размер:** 1.4 × 2.15 × 1.4 м.
- **Описание:** глиняная шахтная печь-домница, сопло для мехов, яма для шлака.
- **Промт:** `Iron bloomery furnace: tall clay shaft furnace 2 m, tuyere hole for bellows, slag pit at the base, early medieval`

#### Рама для дубления — `TanningRack` (пристройка к верстаку)
- **Размер:** 1.7 × 1.8 × 0.35 м.
- **Промт:** `Wooden hide-stretching rack frame with an animal hide laced on it, scraping tool`

---

## 3. Инструменты (в руке и на земле)

Привязка оружия и инструментов: **pivot в месте хвата**, рукоять вдоль **+Y**,
лезвие/голова сверху. Одна модель используется и в руке, и лежащей на земле.
Варианты по материалу (камень → медь → бронза → железо) — одна форма с разными
материалами/цветом металла.

| Предмет | Сейчас | Размер | Промт |
|---|---|---|---|
| **Каменный топор** `StoneAxe` (P2) | LowPolyWeapons `axe1H9` | ~0.6 м | `Primitive stone axe: knapped flint head lashed with leather to a wooden handle` |
| **Медный / бронзовый / железный топор** `CopperAxe`, `BronzeAxe`, `IronAxe` (P2) | та же `axe1H9`, перекрашена | ~0.7 м | `Old Russian bearded axe (borodovidny topor), metal head (copper / bronze / dark iron), ash wood handle` |
| **Кирки** `StonePickaxe`, `CopperPickaxe`, `BronzePickaxe`, `IronPickaxe` | **своя модель** `Art/Models/Tools/Pickaxe.fbx` (уже есть, медь/бронза/железо — перекрашенные копии) | 0.65 м | оставить; при желании — отдельные головы по металлу: `Pickaxe with a [stone / copper / bronze / iron] head on a wooden handle` |
| **Молот строителя** `WoodHammer` | **своя модель** `Art/Models/Tools/Hammer.fbx` (уже есть) | — | оставить |

---

## 4. Броня (на теле и на земле) — P1

Сейчас вся броня — **заглушки из примитивов**, надеваются частями на кости
персонажа: шлем — на голову, корпус — на грудь, ноги — 4 части (бёдра и голени).
Нужно: модель **на теле** (лучше — скиннинг на тот же риг, что у персонажа, иначе
отдельные части по костям) и **предмет на земле** (сложенная/лежащая).
Файлы: `Assets/NAV/Prefabs/Armor/*` (на теле), `Assets/NAV/Prefabs/Item/*_item.prefab` (на земле).

| Набор | Шлем | Грудь | Ноги |
|---|---|---|---|
| **Кожаный** (тир 1) | `LeatherCap` — кожаная шапка с меховой оторочкой | `LeatherTunic` — кожаная куртка-стёганка | `LeatherTrousers` — кожаные штаны с обмотками |
| **Медный** (тир 2) | `CopperHelm` — купольный шлем с наносником | `CopperScaleShirt` — чешуйчатая рубаха из медных пластин | `CopperGreaves` — медные поножи |
| **Бронзовый** (тир 3) | `BronzeHelm` — шлем с нащёчниками | `BronzeCuirass` — бронзовый панцирь | `BronzeGreaves` — бронзовые поножи |
| **Железный** (тир 4) | `IronShelom` — **шелом** (островерхий шлем с бармицей) | `IronChainmail` — кольчуга до колен | `IronGreaves` — железные поножи с наколенниками |

**Промты** (по одному на предмет, + стилевой блок):

```
Leather cap with fur trim, Slavic peasant hunter, worn on head
Padded leather tunic, quilted, laced front, belt
Leather trousers with linen leg wraps
Copper dome helmet with a nose guard, riveted bands
Shirt of overlapping copper scales (scale armour), short sleeves, belt
Copper greaves (shin guards) with leather straps
Bronze helmet with cheek guards and a crest ridge
Bronze cuirass, moulded chest plate with simple ornament
Bronze greaves with knee plates
Old Russian iron shelom: tall pointed conical helmet with aventail (chainmail
neck guard) and a small nasal guard, decorated rim
Knee-length iron chainmail hauberk (kolchuga), short sleeves, leather belt
Iron splinted greaves with knee cops
```

---

## 5. Оружие — P2

Сейчас всё оружие — пак **LowPolyWeapons** (`Assets/LowPolyWeapons/Models/LowPolyWeapons.fbx`,
155 моделей, западноевропейский фэнтези-стиль). В игре с рецептами используются
**только эти 27** — их и нужно заменить на древнерусские. Остальные модели пака
пока лежат без дела (для будущих тиров).

Привязка: pivot в месте хвата, рукоять по **+Y**. Щит — pivot за ручкой, лицевая
сторона по **+Z**.

| В игре | Модель пака | Длина | Промт (+ стилевой блок) |
|---|---|---|---|
| Stone Sword | sword1H10 | 0.8 м | `Crude short sword of sharpened stone/flint blade bound to a wooden hilt` |
| Bone Knife | dagger9 | 0.3 м | `Knife with a carved bone blade and leather-wrapped grip` |
| Copper Dagger | dagger7_001 | 0.35 м | `Copper dagger, leaf-shaped blade, wooden grip` |
| Bronze Dagger | dagger2 | 0.35 м | `Bronze dagger with a cast ornamental hilt` |
| Iron Dagger | dagger3 | 0.4 м | `Iron seax-like knife (nozh), single edge, bone handle` |
| Copper Sword | sword1H2 | 0.9 м | `Copper sword, short broad blade` |
| Bronze Sword | sword1H6 | 0.95 м | `Bronze leaf-blade sword with cast hilt` |
| Iron Sword | sword1H1 | 1.0 м | `Old Russian (Kievan Rus) iron sword, wide fuller, lobed pommel, short crossguard` |
| Iron Greatsword | sword2H1 | 1.4 м | `Two-handed iron sword in Kievan Rus style, long grip, lobed pommel` |
| Copper / Bronze / Iron Hand Axe | axe1H1 / axe1H3 / axe1H2 | 0.6 м | `War axe (sekira) with a narrow bearded head, [copper/bronze/iron]` |
| Iron Battle Axe | axe2H1 | 1.3 м | `Two-handed broad battle axe (berdysh-like) on a long haft` |
| Wooden Club | hammer1H1 | 0.8 м | `Knotty wooden club (dubina), thick root end` |
| Copper / Bronze / Iron Mace | hammer1H2 / 1H9 / 1H4 | 0.7 м | `Flanged mace (pernach/bulava) with a [copper/bronze/iron] head on a wooden handle` |
| Great Club | hammer2H1 | 1.3 м | `Huge two-handed wooden club studded with iron nails` |
| Iron Warhammer | hammer2H4 | 1.3 м | `Two-handed iron war hammer (klevets), spike on the back` |
| Fang Spear | spear1 | 1.9 м | `Spear with a wolf-fang lashed spearhead, wooden shaft` |
| Copper / Bronze / Iron Spear | spear2 / spear4 / spear3 | 2.0 м | `Old Russian spear (kopyo) with a leaf-shaped [copper/bronze/iron] head` |
| Wooden Shield | shield1 | Ø 0.8 м | `Round wooden plank shield with a central iron boss, painted red` |
| Copper Shield | shield11 | Ø 0.8 м | `Round shield with copper rim and boss` |
| Banded Shield | shield13 | Ø 0.85 м | `Round shield reinforced with iron bands, painted folk sun symbol` |
| Bronze Shield | shield14 | 0.9 м | `Teardrop (almond) shield of Kievan Rus, bronze rim and boss, painted` |

---

## 6. Ресурсы и предметы на земле — P1/P2

Лежат на земле после добычи/выпадения, поднимаются. Размер — примерно «чтобы было
видно в траве» (0.2–0.5 м). Pivot — центр основания.

| Предмет | Сейчас | Промт (+ стилевой блок) |
|---|---|---|
| Wood — дерево | Wood Pack `Broken log_1` (P2) | `Small bundle of split firewood logs tied with a rope` |
| Oak Wood — дубовая древесина | **примитив** (P1) | `Bundle of dark oak timber planks / squared logs` |
| Rock — камень | PolyOne `SM_Rocks_01` (P2) | `Small grey stone, rounded, fits in a hand` |
| Flint — кремень | PolyOne `SM_Rocks_11` (P2) | `Flint nodule, dark glassy core, chipped faces` |
| Resin — смола | **примитив** (P1) | `Lump of amber-yellow pine resin` |
| Raw Meat — сырое мясо | **примитив** (P1) | `Raw meat chunk on the bone` |
| Cooked Meat — жареное мясо | **примитив** (P1) | `Roasted meat on the bone, golden-brown crust` |
| Hide — шкура | **примитив** (P1) | `Folded raw animal hide with fur` |
| Leather — кожа | **примитив** (P1) | `Rolled tanned leather tied with a cord` |
| Bone — кость | **примитив** (P1) | `Animal bone, femur-like` |
| Bone Glue — костный клей | **примитив** (P1) | `Small clay pot with bone glue and a wooden stick` |
| Fang — клык | процедурная сетка (P1) | `Wolf fang, curved white tooth` |
| Copper Ore — медная руда | процедурный осколок (P1) | `Chunk of copper ore, rock with green-blue malachite and copper veins` |
| Tin Ore — оловянная руда | процедурный осколок (P1) | `Chunk of tin ore (cassiterite), dark brown-black crystals in grey rock` |
| Bog Iron Ore — болотная руда | процедурный осколок (P1) | `Lump of bog iron ore, rusty brown porous stone` |
| Charcoal — уголь | процедурный осколок (P1) | `Pieces of charcoal, black, with wood grain` |
| Copper / Tin / Bronze / Iron Bar — слитки | **примитивы-кубы** (P1) | `Small cast metal ingot (grivna-shaped bar) of [copper / tin / bronze / iron]` |

---

## 7. Мир

### 7.1 Деревья (рубятся, падают стволом, распадаются на брёвна) — P2

Каждое дерево — 3 стадии: **растущее дерево → поваленный ствол → 2 бревна**, на
месте остаётся **пень**. Стадии ствола/брёвен/пня — общие для всех хвойных и
лиственных (см. 7.2). Дерево масштабируется в игре (0.4–2.4×), поэтому модель
делается в «среднем» размере.

| Дерево | Сейчас | Высота | Промт |
|---|---|---|---|
| Ель — `UNS_Spruce_WoodNode` | Ultimate Nature Starter `UNS_Spruce_01` | 12.5 м | `Norway spruce tree, dense conical crown, drooping branches, 12 m` |
| Лиственница — `HP_Larch_Tree_WoodNode` | Hand Painted Nature Kit LITE `Larch_Tree` | 13 м | `Siberian larch, airy layered crown, reddish bark, 13 m` |
| Кедр — `HP_Cedar_Tree_03_WoodNode` | Hand Painted Nature Kit LITE `Cedar_Tree_03` | 11 м | `Siberian cedar pine, broad dense crown, 11 m` |
| Лиственные деревья (4 шт.) — `NSK_tree01..04` | Nature Starter Kit 2 `tree01–04` | 6–12 м | `Broadleaf trees: linden, maple, rowan with red berries, willow` |
| Берёза (3 варианта) — `Birch_A/B/C` | **процедурная заглушка** (P1) | 10–11 м | `Russian white birch, white bark with black marks, slightly bent trunk, light green airy crown` |
| Старый дуб — `OldOak_WoodNode` | NSK `tree03`, перекрашен (P1) | 14–17 м | `Ancient mighty oak, massive gnarled trunk, wide spreading crown, fairy-tale "Lukomorye" oak` |
| Вековые ель / лиственница / кедр (дремучий лес) | те же модели, затемнены и увеличены до 18–27 м (P1) | 18–27 м | `Giant ancient spruce, mossy trunk 1.5 m thick, dark needles, 25 m, primeval forest` (и аналогично) |

**Требования к деревьям:** pivot у основания ствола; ствол — отдельный простой
коллайдер; листва — Alpha Clip, двусторонняя; LOD0/1/2. Импостеры для дальнего
плана игра запекает сама (меню `NAV > World > Bake Tree Impostors`).

### 7.2 Стадии рубки — P2

| Стадия | Сейчас | Размер | Промт |
|---|---|---|---|
| Пень — `SpruceStump` / `OakStump` | Ultimate Nature `UNS_Stump` | 1.6 × 1.4 м | `Fresh tree stump with axe cut top, roots spreading` |
| Поваленный ствол — `SpruceFallen` / `OakFallen` | Wood Pack `Log_1` (растянут) | 10 м | `Long felled tree trunk without branches, bark, cut end` |
| Бревно — `SpruceLog` / `OakLog` | Wood Pack `Log_1` | 4.6 × 0.6 м | `Round log 4.6 m with bark, both ends sawn` |
| Ветка на земле — `LooseBranch` | Wood Pack `Broken log_1` | 0.7 м | `Fallen dry branch, small twigs` |

### 7.3 Кусты (ломаются руками на равнинах; в лесу — декор) — P2
- **Сейчас:** Nature Starter Kit 2 `bush02–bush06` (5 видов). `World/Vegetation/NSK_bush0X(_WoodNode).prefab`.
- **Размер:** 1–2 м. **Промт:** `Low wild bushes of the Russian forest: hazel, wild rose with pink flowers, raspberry, juniper, willow shrub`

### 7.4 Камни и рудные жилы — P1/P2

| Объект | Сейчас | Размер | Промт |
|---|---|---|---|
| Валун (добывается киркой) — `SM_Rocks_03` | PolyOne Rocks Stylized (P2) | 4.4 × 1.8 × 3.2 м | `Large mossy granite boulder, stylized` |
| Мелкий камень на земле — `LooseStone` | PolyOne `SM_Rocks_01` (P2) | 0.3 м | `Small pebble stone` |
| Кремнёвая порода — `Flint_Ore_Rock_01` | Ultimate Nature `UNS_Standard_Rock_01` (P2) | ~2 м | `Chalky rock outcrop with dark flint nodules` |
| Медная жила — `CopperOre_Rock` | UNS rock + процедурные осколки (P1) | 2.0 × 1.4 × 1.8 м | `Rock outcrop with green malachite and copper veins` |
| Оловянная жила — `TinOre_Rock` | то же (P1) | ~2 м | `Shore rock with dark cassiterite crystal clusters` |
| Болотная руда — `BogIron_Rock` | то же (P1) | ~2 м | `Rusty bog iron deposit, reddish-brown porous lumps in mud` |

Жилы должны **уменьшаться/раскалываться** при ударах — удобно, если модель
состоит из 2–3 частей (порода + кристаллы).

### 7.5 Декор (не добывается) — P3

| Объект | Сейчас (ассет) | Промт |
|---|---|---|
| Папоротник — `Fern` | Hand Painted Nature Kit LITE | `Forest fern clump` |
| Белый гриб — `Boletus_Edulis` | Hand Painted Nature Kit LITE | `Porcini mushroom group` |
| Одуванчик — `Dandelion_02` | Hand Painted Nature Kit LITE | `Dandelion with yellow flower and seed head` |
| Подорожник — `Plantain` | Hand Painted Nature Kit LITE | `Plantain leaves rosette` |
| Пень (декор) — `Stump` | Hand Painted Nature Kit LITE | `Old mossy rotten stump` |
| Грибы — `UNS_Mushroom_Patch` | Ultimate Nature Starter | `Patch of small forest mushrooms, fly agarics` |
| Пень — `UNS_Stump` | Ultimate Nature Starter | `Weathered tree stump` |
| Бревно — `UNS_Log` | Ultimate Nature Starter | `Fallen mossy log` |
| Камешки — `UNS_Tiny_Rock_01` | Ultimate Nature Starter | `Small river pebbles` |
| Ромашка — `ChamomileDetail` | **процедурная** (P1) | `Chamomile flower clump (low-poly, alpha cards)` |
| Василёк — `CornflowerDetail` | **процедурная** (P1) | `Blue cornflower clump (low-poly, alpha cards)` |
| Трава (текстуры) | Nature Starter Kit 2 `grass01/02` (перекрашены) | `Grass blade texture atlas, alpha, stylized` |

---

## 8. Не модели, но тоже из ассетов (для полноты)

| Что | Ассет | Примечание |
|---|---|---|
| Небо | **Fantasy Skybox FREE** (`FS003_Day_Sunless` + набор FS003) | панорамы неба, смена по времени суток |
| Вода | свой шейдер `NAV/Water` на плоскости (+ импортирован SUIMONO, не используется) | модель не нужна |
| Земля: равнины, березняк | Ultimate Nature `UNS_Terrain_Grass` | текстура 2D |
| Земля: лес, дремучий лес | Nature Starter Kit 2 `ground03` | текстура 2D |
| Земля: берег/дно | ADG_Textures `ground2` | текстура 2D |
| Иконки предметов | рендерятся из моделей автоматически | после замены модели иконки пересоздаются |
| Отладка | `Dev/WeaponRack` — примитивы | не нужен, в игре не используется |

---

## 9. Анимации и подвижные части

### 9.1 Игрок — анимации уже есть, делать не нужно

Анимации взяты из паков **Kevin Iglesias** (Human Basic Motions, Melee Warrior) и
**Brawler Animations** и подключены в `PlayerAnimatorController`. Новая модель
персонажа должна просто подойти к ним: **Humanoid-риг Unity**, стандартная
раскладка костей (бёдра → позвоночник → грудь → шея → голова, плечи, руки, пальцы,
ноги), T-поза, кости для предметов в кистях (`B-handProp.R`, `B-handProp.L`).

Для справки — что сейчас играет персонаж:

| Группа | Клипы |
|---|---|
| Ходьба (8 направлений + стойка) | Idle01, Walk01_Forward / Backward / Left / Right / ForwardLeft / ForwardRight / BackwardLeft / BackwardRight |
| Спринт | Sprint01_Forward / Left / Right / ForwardLeft / ForwardRight, Run01_Backward / BackwardLeft / BackwardRight |
| Прыжок / падение | Fall01 |
| Стойки с оружием (верх тела) | одноручное, двуручное, древковое; переноска двуручного и древкового |
| Удары | кулаками (джеб левой/правой, хук), одноручным (слева, справа, сверху), двуручным (рубящий, зеркальный), копьём (укол, зеркальный, удар сверху), щитом (удар кромкой, толчок), удар молотом при строительстве |
| Кисти | сжатый кулак при хвате предмета (отдельно левая и правая) |

Если броня делается **скиннингом на тот же риг** — она двигается с этими анимациями
сама. Если отдельными жёсткими частями — каждая крепится к своей кости: шлем —
голова, корпус — грудь (Spine/Chest), бёдра — UpperLeg, голени — LowerLeg.

### 9.2 Звери — анимации нужно сделать

Сейчас у оленя и волка (тигра-заглушки) из пака ithappy есть **только idle, walk,
run** (у тигра ещё «редкий idle»). **Атаки и смерти нет** — игра изображает их
программно: атака — короткий рывок модели вперёд с наклоном головы, смерть —
модель переворачивается на бок и замирает. Это временно.

Требования ко всем зверям: **Generic-риг**, корень в pivot (на земле), анимации
«на месте» (**in-place, без root motion** — перемещает игра), клипы отдельными
дублями в FBX или отдельными файлами. Скорость шага в клипе указать (м/с), игра
подгоняет темп под реальную скорость.

| Клип | Олень | Волк | Как используется в игре |
|---|---|---|---|
| idle (цикл) | ✔ | ✔ | стоит на месте |
| idle_rare (вариация) | ✔ (поднимает голову, прислушивается) | ✔ (чешется, нюхает) | изредка вместо idle |
| graze (цикл) | ✔ (ест траву) | — | мирное ожидание |
| walk (цикл) | ✔ ~1 м/с | ✔ ~1.5 м/с | блуждание |
| run (цикл) | ✔ ~7 м/с | ✔ ~7 м/с | бегство / погоня |
| attack | — | ✔ укус в рывке, 0.6–0.8 с, момент укуса отметить | атака игрока |
| hit | ✔ | ✔ | вздрагивание при попадании, 0.3 с |
| death | ✔ | ✔ | падение на бок, последний кадр — лежит |
| howl | — | ✔ | вой ночью (атмосфера) |

Сейчас аниматор зверей управляется двумя параметрами: `Vert` (0 = стоит,
1 = движется) и `State` (0 = шаг, 1 = бег). Новые анимации можно сдать просто
клипами — контроллер и связь с игрой мы настроим сами.

### 9.3 Подвижные части построек и предметов (не скелетная анимация)

Эти части двигает игра — от художника нужно только, чтобы они были **отдельными
объектами с правильно поставленной точкой вращения**:

| Объект | Что двигается | Точка вращения |
|---|---|---|
| Дверь `WoodDoor` | створка, открывается на ~100° | вертикальная ось на левом краю проёма, низ створки |
| Ворота `OakGate` | две створки, по 95° в разные стороны | вертикальные оси на внешних краях |
| Сундук `WoodChest` | крышка | горизонтальная ось вдоль задней кромки |
| Рудные жилы, камни | уменьшаются при каждом ударе, затем исчезают | pivot в центре основания |
| Деревья | при срубе ствол падает (физика), затем распадается на 2 бревна | pivot ствола у основания; бревно — pivot в центре |
| Костёр, кузня, печи | огонь, искры, дым — эффекты Unity, моделировать не нужно; оставить место под пламя | — |

---

## 10. Порядок работы (рекомендация)

1. **Волк** (сейчас стоит тигр) и **игрок**.
2. **Базовые постройки**: фундамент, стена, дверь, крыша, фронтон, частокол.
3. **Станции**: верстак, костёр, сундук, кузня, наковальня, мехи, печи.
4. **Ресурсы на земле** (слитки, руды, мясо, шкура и т. д.) и **рудные жилы**.
5. **Броня** (4 набора × 3 части).
6. **Оружие** (27 моделей) и **топоры**.
7. **Берёза, старый дуб, вековые деревья**, затем остальные деревья и кусты.
8. Дубовый набор.
9. Декор.

Готовые модели — присылать `.fbx` + текстуры; при сдаче указывать, на какую
модель из этого списка заменить (по имени в скобках, например `WoodWall`).

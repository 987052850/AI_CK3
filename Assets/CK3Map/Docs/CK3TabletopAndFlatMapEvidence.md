# CK3 桌面、远景手绘地图与世界外侧底图证据

状态：研究阶段已闭合默认西式桌面的静态输入与远景手绘图核心 Shader 链；Unity 阶段 13 的静态构建入口已实现，等待 Scene/Game 验收。

## 1. 已确认的三层结构

| 内容 | 原版证据 | 等级 | 结论 |
|---|---|---|---|
| 默认桌面样式 | `game/gfx/map/table_styles/table_styles.txt` | A | `map_table_style_western` 是默认样式，并使用 `environment_western_table.txt`。CE1、EP3、TGP 是条件式替代样式。 |
| 桌子及周边装饰 | `game/gfx/map/map_object_data/map_table_western.txt` | A | 桌体、蜡烛、桌布、道具是四个独立实体，全部使用 `render_pass=MapUnderTerrain`，不是一张背景图。 |
| 桌面缩放范围 | `game/gfx/map/map_object_data/layers.txt` | A | `map_table_layer_western` 的 `fade_in=21`、`fade_out=80`，并由 `map_table_style_western` 可见性标签控制。 |
| 远景手绘地图 | `game/gfx/map/terrain/flat_maps/flatmap.dds`、`game/gfx/FX/pdxterrain.shader`、`game/gfx/FX/pdxwater.shader` | A | 手绘图是 `TerrainFlatMap` 纹理，同时混入 Terrain 和 Water；它不是放在地形下面的独立 Quad。 |
| 手绘图出现档位 | `game/gfx/map/map_modes/map_modes.txt` | A | `@zoom_step_flat_map=21`，第 20 档为过渡前档，第 21 档进入 Flat Map。 |
| 撕纸式过渡 | `game/gfx/FX/paper_transition.fxh` | A | `paper_tear_mask.dds` 的 RGB 在三种 UV 尺度采样，以阈值和 `smoothstep` 生成不规则纸张混合边缘。 |
| 世界矩形外侧 | `game/gfx/FX/surroundmap.shader`、`game/gfx/map/surround_map/*` | A | 地图外侧另有 Surround Map；它使用独立遮罩、淡出图与云层/阴影逻辑，不等于 `flatmap.dds`。 |

## 2. 默认西式桌面的原版对象

`map_table_western.txt` 给出四个单实例对象，坐标与缩放如下：

| 原版对象 | 实体 | Position | Scale |
|---|---|---|---|
| `western_tabletop` | `tabletop_west_basic_entity` | `(4500,-15,2560)` | `(5,5,5)` |
| `western_tabletop_candles_01` | `tabletop_west_basic_candles_entity` | `(4500,-1,2560)` | `(5,5,5)` |
| `western_tabletop_cloth` | `tabletop_west_basic_tablecloth_entity` | `(4500,-20,2560)` | `(5,5,5)` |
| `western_tabletop_props_01` | `tabletop_west_basic_props_entity` | `(4500,-1,2560)` | `(5,5,5)` |

四个实体分别由以下原版资产定义：

- `game/gfx/models/tabletop/tabletop_west_basic.mesh/.asset`
- `game/gfx/models/tabletop/tabletop_west_basic_candles.mesh/.asset`
- `game/gfx/models/tabletop/tabletop_west_basic_tablecloth.mesh/.asset`
- `game/gfx/models/tabletop/tabletop_west_basic_props.mesh/.asset`

桌体资产包含地面阴影面、椅子、盾牌、木桌与金属边条。道具资产包含剑、杯、匕首、书、硬币、钱袋、卷轴、箱子、羽毛笔、书写工具、棋子和木雕。蜡烛实体还通过 attachment 引用火焰与光晕实体。桌布包含 `tabletop_west_basic_tablecloth_idle.anim` 动画。

静态 Mesh、子网格材质和 DDS 均有 A 级输入；桌布动画、蜡烛火焰 attachment/VFX 的 Unity 等价承载尚未实现，不能在首个静态版本中声称已经完整复刻。

## 3. Flat Map 与 Surround Map 规格

- `flatmap.dds`：`9216×4608`、DXT1、完整世界手绘图。
- `flatmap_tgp.dds`：`9216×4608`、DXT1、东亚桌面样式变体。
- `paper_tear_mask.dds`：`1024×1024`、DXT1、11 Mip。
- `surround_mask.dds`：`4096×2048`、DXT1、13 Mip。
- `surround_fade.dds`：`1152×576`、DXT5、11 Mip。

`pdxterrain.shader` 在 Flat Map 过渡时同时执行两件事：顶点高度向 `FlatMapHeight` 插值，像素颜色通过 `CalculatePaperTransitionBlend` 向 `TerrainFlatMap` 插值。`pdxwater.shader` 对水色执行同一张 Flat Map 和同一撕纸混合。只在 Terrain 上铺一张手绘纹理而不处理 Water 和高度，不是原版方案。

`surroundmap.shader` 在 `FlatMapHeight` 构建世界外侧表面，使用 `surround_mask` 区分地图、云与阴影，并随 `FlatMapLerp` 改变透明度。原 Shader 已注释说明旧的 woodgrain 平铺不再使用，因此不能把 `surround_tile.dds` 当成当前桌面木纹核心。

## 4. 原版桌面环境

`game/gfx/map/environment/environment_western_table.txt` 确认：

- Cubemap：`gfx/portraits/environments/castle_interior_01_fire.dds`
- `sun_intensity=5`
- `sun_color=hsv{0.08,0.15,1}`
- `sun_direction={-3.3,1.3,-1}`
- `exposure=2`、`contrast=1.1`、`pivot=0.12`
- Tonemap：`TonyMcMapface`
- Bloom：宽度 `0.5`、强度 `0.75`、阈值 `0.4`

这些数值和来源是 A 级事实；Clausewitz 光照单位、TonyMcMapface 与 Unity URP Volume 并非同一实现。Unity 中可先保留数据字段并使用独立适配层，不能声称把数值原样填入 URP 就获得完全相同结果。

## 5. Unity 导入顺序

1. 扩展原版数据同步范围，加入 `game/gfx/models/tabletop`，并登记桌面环境引用的 Cubemap；源数据保持原始字节与相对路径。由于 Unity 将 `.mesh/.anim/.asset/.meta` 作为自身保留格式，前三种在 Source 镜像中追加 `.ck3source` 载体后缀，原版 `.meta` sidecar 不进入 Assets；这只改变 Unity 存储名，不改变来源字节。
2. 在 Editor 中解码四个西式桌面 PDX Mesh 的全部子网格；材质必须按相邻 `.asset` 的 `meshsettings` 绑定 diffuse、normal、properties 和 detail 纹理。
3. 在 Scene 中保存一个静态桌面根对象及四个子对象，使用 `map_table_western.txt` 的原始 Transform；不在 Play 模式临时创建。
4. 将 `flatmap.dds` 与 `paper_tear_mask.dds` 以原始块数据生成持久 Texture 资产，在 Terrain 和 Water Shader 中共同接入 `FlatMapLerp`、高度压平与撕纸过渡；终点档位为 21。
5. 单独构建 Surround Map 表面和 Shader，使用原始 mask/fade 数据；不要把它合并进桌体材质或 Flat Map。
6. 首次验收只覆盖默认西式静态桌面、完整世界 Flat Map 与 Surround Map。桌布动画、蜡烛火焰、CE1/EP3/TGP 桌面变体在静态链验收后继续。

## 6. 当前实现与缺口

- 原版同步范围已加入 `game/gfx/models/tabletop` 与西式桌面环境 Cubemap。
- `tabletop_west_basic_candles.mesh` 的字段序列已定点验证：三个完整 `p/n/ta/u0/tri` 几何块之后还有六个 `p[3]` attachment/locator 位置记录。解码器只把带 `tri` 的 `p` 块登记为子网格，不再把 attachment 误报为损坏几何。
- `CK3TabletopFlatMapBuilder` 复用同一个 PDX Mesh 解码器，保存四组完整多子网格 Mesh、材质和原始 Transform；没有把桌面拆成手工 Primitive。
- Terrain 与 Water 已绑定同一份 `flatmap.dds` 和 `paper_tear_mask.dds`，缩放仍由现有 `CK3MapModeController` 在原版 20→21 档同步驱动。
- `CK3SurroundMap.shader` 当前移植原版 `PS_surroundmap_flat`。
- 多子网格 detail、alpha-to-coverage、map-floor 和 tabletop 专用材质分支尚未逐项移植；桌布骨骼/动画与蜡烛 attachment/VFX 也尚未闭合。

## 7. 桌面材质依赖与光照修复（2026-08-05）

- 四组桌面 `.asset` 的 `meshsettings` 是逐子网格材质绑定的权威输入；其中的 diffuse、normal、specular 和 shader 必须覆盖 PDX Mesh 中同名但不同用途的内嵌贴图字段。例如桌面剑明确覆盖为 `ep1_western_sword_01_table_*`。
- 桌面依赖并不全部位于 `gfx/models/tabletop`。椅子、书、箱子、匕首、盾牌、棋子、卷轴、钱袋、羽毛笔和第三组蜡烛分别引用 `gfx/models/artifacts` 与 `gfx/models/court` 下的原版纹理。因此同步器已加入这些确定依赖目录，材质构建器在完整 Source 镜像中按文件名解析依赖。
- 生成桌面材质时，diffuse、normal 或 properties 任一缺失都会终止构建并报告具体子网格，不再让 Shader 的白色默认纹理掩盖导入错误。
- `CK3TabletopSurface.shader` 使用原版 properties 通道：A 为感知粗糙度、G 为镜面强度、B 为金属度；直接光和环境反射沿用已移植的 CK3 GGX/IBL 计算，并绑定 `castle_interior_01_fire.dds`、原版太阳颜色与强度。

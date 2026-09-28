# CK3 世界地图严格复刻规范与证据登记

## 0. 项目目标

本项目在 Unity 2022.3.62f1c1 URP 中复刻《Crusader Kings III》的完整世界地图。目标不是制作“类似 CK3”的地图，而是尽可能忠实移植能够由 CK3 本体数据、配置和 Shader/FXH 直接证明的实现。

最终成果必须覆盖 CK3 原版完整地图范围并使用完整世界数据。局部区域只能在开发过程中用于诊断单个算法或 Shader，不得成为最终数据管线、最终场景或缩减世界范围的理由。任何导入器、坐标系统、分块系统、材质控制图、政治数据、边界、河流、海洋和标签实现都必须从一开始支持完整世界，不能先建立一个只适用于裁剪区域的架构。

近景装饰已进入样品验证阶段：先验证原版树木、城堡、城市、神殿与山石的专有 Mesh 解码、纹理导入和 Scene/Game 承载；道路、军队与地图图标仍在后续阶段。

### 0.1 当前执行与验收节奏

项目约束的唯一完整来源是仓库根目录 `AGENTS.md`；本 README 只记录原版证据、项目架构、当前进度和阶段验收，不再复制一整套容易过期的工作规则。

后续默认采用 IMPLEMENT 阶段工作流：先读取第 15 节唯一有效进度表和已有证据库，只补齐会阻塞当前阶段的证据；随后完成一个 20–40 分钟内可见、可验收的最小阶段成果。每个阶段依次遵循“证据与输入闭合 → 最小可见结果 → 局部样本验证 → 完整世界数据 → 原版细节闭合 → 性能优化”。局部验证不得降低完整世界地图的最终目标。

当阶段成果已经可在 Unity 中检查时应及时交给用户验收，避免连续数小时沿错误方向扩展。未知细节不得猜测为原版事实；不阻塞当前阶段的未知细节则记录为待确认项，或使用明确标记且可替换的 Unity 载体适配，不得因此无限扩大研究范围。

原版事实只以 A 级证据进入正式复刻。B 级只能用于清楚标记的推断或可替换载体，C 级不能进入严格复刻实现，D 级必须获得用户明确同意。实施前优先搜索 `Assets/CK3Map/Docs/`、本 README 和 `Assets/CK3Map/Docs/CK3OriginalBinaryEvidenceIndex.md`；已有证据足够时禁止重复考古。

## 1. 绝对证据准入规则

### 1.1 先确认，后实现

修改前必须确认采用的是 CK3 原版数据和原版实现方式。证据可以来自本体数据、配置、Shader/FXH、地图编辑器资源或 `ck3.exe` 静态分析。关键实现尚未确认时继续查找证据，不用自制近似方案补位。

### 1.2 禁止推测

以下理由均不能成为实现依据：

- 一般策略游戏或 GIS 通常这样做。
- Unity 中这样更方便。
- 截图看起来可能是这样。
- 数学上应该等价。
- 先做一个相似版本，以后再替换。

推测只能列入待调查问题，不能进入代码、Shader、资产生成器或最终技术结论。

### 1.3 引擎内部实现缺失

若关键算法位于未提供的 Clausewitz/Jomini C++ 内部：

1. 记录已经确认的输入和输出接口。
2. 标记“原版引擎内部实现未确认”。
3. 停止该算法的复刻。
4. 不得编写一个看起来相似的替代算法。

Unity API 可以承载已经确认的原版算法和渲染状态，但不能借“引擎不同”改变数据含义或处理原则。

若缺失项不阻塞当前阶段，允许按 `AGENTS.md` 第 7 节使用独立、可替换且明确标记的 Unity 载体来验证已确认的数据输入输出；这不等于补写原版算法，也不得作为原版实现结论。

### 1.4 全根目录证据普查

CK3 的地图实现分布在三棵目录树中：

- `game/`：CK3 游戏层数据、配置、Shader 和对 Jomini 文件的覆盖版本。
- `jomini/`：Jomini 地图系统、政治覆盖、距离场、河流、水体、地图名称和地图编辑器资源。
- `clausewitz/`：Clausewitz 底层 Terrain、Heightmap、纹理/法线工具函数和通用渲染代码。

任何“缺少原版实现证据”的结论必须在三棵目录树全部完成以下检索后才能成立：

1. 按文件名和目录名搜索。
2. 追踪 Shader 的全部 `Includes`。
3. 搜索关键函数定义。
4. 搜索关键函数调用者。
5. 比较 `game/gfx/FX/jomini` 与 `jomini/gfx/FX/jomini` 的同名文件，确认 CK3 覆盖版本。
6. 检查地图编辑器 GUI 和工具资源是否暴露了引擎数据结构或操作模式。

2026-07-15 的首次全根目录复查纠正了早期遗漏：`clausewitz/gfx/FX/cw/pdxterrain.fxh`、`clausewitz/gfx/FX/cw/heightmap.fxh` 和 `jomini/gfx/FX/jomini/jomini_unsigned_distancefield.shader` 均存在。此前关于这些实现文件不存在的结论作废。

## 2. Unity 传统工作流约束

- 静态 Terrain Mesh、Collider Mesh、边界 Mesh、河流 Mesh、距离场、导入纹理、材质和标签布局数据必须由显式 Editor 工具创建并保存到 `Assets`。
- 地图必须在非 Play 模式的 Scene 视图中可见，Scene 与 Game 使用同一套持久化资产。
- Runtime 禁止为静态地图执行 `new Mesh()`、`new Texture2D()`、`new Material()`、克隆 Mesh 或创建地图分块对象。
- Runtime 只负责摄像机、选择、悬停、地图模式等动态状态。
- 项目自定义 Inspector 参数使用中文名称和中文 Tooltip；Unity/URP 已有组件直接使用，不重复包装。
- 参数修改应通过材质属性、`OnValidate` 或安全的 Editor 刷新即时预览；需要重建的参数必须明确标记“重建后生效”。
- 一个构建流程只保留一个明确入口，必须支持输入校验、Undo、进度、确定输出路径、资产保存和场景 Dirty。

## 3. 总体数据坐标与分辨率

已确认入口：`game/map_data/default.map`。

该文件直接指定：

- `definitions = "definition.csv"`
- `provinces = "provinces.png"`
- `rivers = "rivers.png"`
- `topology = "heightmap.heightmap"`
- `adjacencies = "adjacencies.csv"`
- `seasons = "seasons.txt"`

已确认的主要源图尺寸：

| 数据 | 尺寸 | 已确认用途 |
|---|---:|---|
| `heightmap.png` | 18432 × 9216 | 原始高程源图 |
| `provinces.png` | 9216 × 4608 | 省份颜色 ID 图 |
| `rivers.png` | 9216 × 4608 | 河网输入图 |
| `detail_index.tga` | 9216 × 4608，32 bit | 每个像素的四个地表材质索引 |
| `detail_intensity.tga` | 9216 × 4608，32 bit | 对应四个材质的强度/权重 |
| `colormap.dds` | 9216 × 4608 | 地表宏观颜色图 |
| `foam_map.dds` | 4608 × 2304 | 海洋泡沫空间分布输入 |
| `watercolor_rgb_waterspec_a.dds` | 4608 × 2304 | RGB 水色，A 水体高光输入 |

坐标翻转必须逐个按原 Shader 证据处理。例如地形 `ColorTexture` 和水体世界 UV 中可以直接看到 Y 翻转，不能给所有源图统一猜测一种翻转规则。

## 4. 基础 Terrain 高度与 Mesh

### 4.1 已确认的高程存储

证据文件：

- `game/map_data/heightmap.heightmap`
- `game/map_data/heightmap.png`
- `game/map_data/packed_heightmap.png`
- `game/map_data/indirection_heightmap.png`
- `game/map_data/nodes.dat`
- `game/gfx/map/terrain/settings.terrain`
- `game/gfx/FX/pdxterrain.shader`
- `clausewitz/gfx/FX/cw/pdxterrain.fxh`
- `clausewitz/gfx/FX/cw/heightmap.fxh`
- `clausewitz/gfx/FX/cw/utility.fxh`

`heightmap.heightmap` 直接确认：

- 原始高程尺寸为 `18432 9216`。
- 实际运行输入为 `packed_heightmap.png`。
- 使用 `indirection_heightmap.png` 做瓦片间接寻址。
- `tile_size = 65`。
- `should_wrap_x = no`。
- `max_compress_level = 4`。
- 文件给出了各级 `level_offsets` 和 `empty_tile_offset`。

`settings.terrain` 直接确认：

- `normal_height_scale = 0.8`
- `normal_step_size = 1.6`
- `skirt_height_factor = 0.1`

`pdxterrain.shader` 的顶点入口接收 `WithinNodePos`、`NodeOffset`、`NodeScale`、`LodDirection` 和 `LodLerpFactor`，调用 `CalcTerrainVertex` 得到世界坐标；另有独立 Skirt 顶点路径调用 `FixPositionForSkirt`。这证明原版使用节点化 LOD 地形，并用裙边处理分块/LOD 接缝。

对本体 `nodes.dat` 与 `ck3.exe` 的交叉复查进一步确认：

- `nodes.dat` 为 `44,739,232` 字节，能够精确拆分为 `1,398,101` 个 32 字节记录。
- `1,398,101 = 1 + 4 + 4^2 + ... + 4^10`，记录开头三个 `float` 依次呈现节点 X、Y 和 Scale，例如根节点 `(0,0,1)`，随后是四个 `(0/0.5, 0/0.5, 0.5)` 子节点，再进入 `0.25` 层级；文件顺序与完整十级四叉树吻合。
- `ck3.exe` 在 `pdx_terrain_quadtree.cpp` 相关字符串附近直接引用 `map_data/nodes.dat`，并提示该文件由 Map Editor 的 Save As 勾选 nodes 重新生成。
- 同一段原版二进制证据给出 `CalculateAbsoluteGeometricError`、`CalculateNextLevelGeometricError`、`CullAndLod`，以及 `Terrain.Quadtree.UseLodLerp`、`UseCache`、`ForceLodLevel`、`LodSwitchFactor`、`ExtraTessellation`、`ScreenSpaceErrorThreshold`、`LeafLevelFadeFactor`、`ScreenHeight`、`DistanceLodFactor` 等原版系统入口。

因此 `nodes.dat` 不是可忽略缓存，而是原版 Terrain Quadtree 的预生成节点/几何误差数据。其 32 字节记录中除 X/Y/Scale 之外字段的精确结构仍需继续确认，禁止按数值外观猜测字段名称。

### 4.2 已确认的 GPU Terrain 顶点与高度页表

`clausewitz/gfx/FX/cw/heightmap.fxh` 直接确认：

- `HeightLookupTexture` 使用 Point 过滤，`PackedHeightTexture` 使用 Linear 过滤。
- 世界坐标先通过 `WorldSpaceToLookup` 进入 Indirection 坐标，并限制到 `[0, 0.999999]`。
- Indirection 的 RG 是瓦片位置，B 参与当前瓦片尺寸计算，A 选择 `TileToHeightMapScaleAndOffset` 压缩层级。
- 当前瓦片使用半 texel 偏移和有效区域缩放，再映射到 Packed Heightmap。
- 最终高度是 Packed Heightmap R 通道乘 `HeightScale`。
- 跨页边界的多重采样会通过九次 `GetHeight01` 保持正确页表寻址；同页时直接在 Packed Heightmap 中九点采样。
- 文件还提供 Bicubic Lagrange 高度函数，但是否用于主 Terrain Pass 必须以调用关系为准，不能默认启用。

`clausewitz/gfx/FX/cw/pdxterrain.fxh` 直接确认：

- `CalcTerrainVertex` 根据 `WithinNodePos`、`NodeOffset` 和倒数 `NodeScale` 得到 Quadtree Position。
- 世界 XZ 由 `NormQuadtreeToWorld` 转换，并限制到 `WorldExtents`。
- 当前高度与沿 `LodDirection` 两侧高度的平均值按 `LodLerpFactor / UINT16_MAX` 插值。
- `FixPositionForSkirt` 按 Vertex ID 奇偶使用 `SkirtSize` 改变 Y。
- Terrain 法线从 X/Z 两侧高度差构造，并乘 `NormalScale` 后归一化。

节点实例流的坐标编码也可由 `nodes.dat` 与 `CalcTerrainVertex` 直接交叉确认：`nodes.dat` 保存归一化绝对偏移 `(X,Y)` 和归一化节点尺寸 `Scale`；而 Shader 会先执行 `NodeScale = 1 / NodeScale`，再执行 `NodeOffset *= NodeScale`。因此提交给 `NodeOffset_Scale_Lerp` 的 XYZ 必须为 `(X/Scale, Y/Scale, 1/Scale)`，Shader 才会严格还原 `X + WithinNodePos.x * Scale` 与 `Y + WithinNodePos.y * Scale`。Unity 节点 Transform 只用于提供与该绝对输出位置一致的持久化对象包围盒，不再次参与 Shader 的世界坐标计算。

2026-07-15 对本体三张高度图进行了逐像素交叉复核，补充确认：

- `heightmap.png` 为 18432×9216 的 16-bit 灰度 PNG，`packed_heightmap.png` 为 3185×4061 的 16-bit 灰度 PNG，`indirection_heightmap.png` 为 288×144 的 RGBA8 PNG。
- `heightmap.heightmap` 的 `tile_size=65`、`max_compress_level=4`、五级 `level_offsets` 依次为 `(0,0)`、`(0,1397)`、`(0,3129)`、`(0,3690)`、`(0,3861)`，`empty_tile_offset=(225,39)`。
- Indirection 的 B 通道只出现 `1/2/4/8/16`，A 通道只出现 `0/1/2/3/4`，二者与 `heightmap.fxh` 的压缩层级和瓦片尺寸公式一致。
- 原图、Indirection 和 Packed Heightmap 都以 PNG 底行对应世界 Y=0，X 不翻转。按 `heightmap.fxh` 页表公式在五个压缩层级抽样，排除刻意落在共享页边的测试点后，Packed Heightmap 与原始 `heightmap.png` 像素完全一致。
- 由原版文件尺寸和 `heightmap.fxh` 公式可直接确定：`WorldSpaceToLookup=(1/9216,1/4608)`、`OriginalHeightmapToWorldSpace=(0.5,0.5)`、`HeightScale=50`；每级 `TileToHeightMapScaleAndOffset` 使用 `currentTileSize=64/(1<<level)+1` 与上述原版 `level_offsets`。

Unity 中新增的 `CK3TerrainHeightBuilder` 只在 Editor 中解码并持久化上述原版输入：输出 3185×4061 R16 Linear/Bilinear/Clamp 压缩高度纹理、288×144 RGBA32 Linear/Point/Clamp 间接寻址纹理，以及保存五级常量的 `CK3原版高度页表.asset`。它不生成 Terrain Mesh，也不在 Runtime 创建纹理。代码已通过 Unity 2022.3.62f1c1 自带 Roslyn 编译；用户于 2026-07-15 在 Unity 中完成构建并确认输出尺寸、格式、五级压缩层级与资产路径正确，本阶段高度页表资产验收通过。

### 4.3 CPU Terrain 当前证据边界

现在已经确认原版 CPU 实现位于编译进 `ck3.exe` 的 `clausewitz/pdx_terrain/pdx_terrain.cpp` 与 `pdx_terrain_quadtree.cpp`，并确认 `nodes.dat`、十级完整四叉树、几何误差计算和 Cull/LOD 系统入口。以下字段级细节仍未从公开脚本、Shader 或二进制字符串中恢复：

- 四叉树 32 字节记录剩余字段的精确布局和读取规则。
- CPU Cull/LOD 的完整判断公式与节点提交顺序。
- 每个节点基础 Mesh 的准确顶点/索引顺序。
- `NodeOffset_Scale_Lerp`、`LodDirection` 的 CPU 填充过程。
- Skirt Mesh 如何复制顶点以及 Vertex ID 的完整布局。
- 从原始 `heightmap.png` 制作 Packed Heightmap 与 Indirection Heightmap 的离线压缩器内部代码；Map Editor 已确认提供 Repack Heightmap，并允许 tile size 33/65/129。

因此可以忠实移植已确认的 GPU 高度页表和顶点计算，但在找到 CPU 证据前，不能声称自制 Quadtree 调度或节点拓扑就是原版实现。

## 5. 自动地表材质渲染

### 5.1 不是按高度自动分类

CK3 并不是在 Shader 中根据高度或坡度自动决定“沙地、草地、雪山”。地表类别由绘制好的材质索引和权重数据决定，高度主要负责几何、法线和部分效果的坡度响应。

证据文件：

- `game/gfx/map/terrain/materials.settings`
- `game/gfx/map/terrain/settings.terrain`
- `game/gfx/map/terrain/detail_data.settings`
- `game/gfx/map/terrain/detail_index.tga`
- `game/gfx/map/terrain/detail_intensity.tga`
- `game/gfx/map/terrain/colormap.dds`
- `game/gfx/map/terrain/masks/*`
- `game/gfx/FX/pdxterrain.shader`
- `game/gfx/FX/dynamic_masks.fxh`
- `game/gfx/FX/province_effects.fxh`
- `clausewitz/gfx/FX/cw/pdxterrain.fxh`
- `clausewitz/gfx/FX/cw/utility.fxh`

`materials.settings` 当前登记 105 个材质 ID。每个地表材质通常明确引用：

- `diffuse`：颜色纹理，Alpha 参与高度混合。
- `normal`：细节法线。
- `material`：材质属性纹理。
- `mask`：对应的源遮罩。
- `id`：稳定材质 ID。
- 可选 `tile_factor`：覆盖默认平铺频率。

其中可以直接找到 beach、desert、drylands、farmland、floodplains、forest、hills、mountain、snow 等原版材质组。

### 5.2 四层选择与高度混合

`detail_data.settings` 确认 `materials_limit = 4`。`pdxterrain.shader` 确认：

1. `DetailIndexTexture` 以 Point 过滤读取四个材质索引。
2. `DetailMaskTexture` 以 Point 过滤读取对应四个权重。
3. Shader 手工收集相邻四个 texel 中相同材质索引的贡献，实现索引图上的双线性权重过渡，而不是直接对离散 ID 做线性过滤。
4. 根据每层 diffuse Alpha 与权重调用 `CalcHeightBlendFactors`，使用 `DetailBlendRange` 做高度混合。
5. 使用相同混合因子合成 diffuse、normal 和 material 属性。
6. 细节纹理来自 2D Texture Array，采用线性和 Mipmap 过滤并重复寻址。

全根目录复查进一步确认：

- `PackedDetailTileFactors[DetailIndex]` 为每个材质提供独立 tile factor。
- 105 套 diffuse、normal、properties 全部为 1024×1024、DXT5、11 级 Mipmap，三组各 105 张。
- 法线为 RRxG 编码；`UnpackRRxGNormal` 从 G 和 A 读取 XY，Y 取反，再由 `sqrt(saturate(1-x²-y²))` 重建 Z。Unity 不得把这些纹理交给普通 Normal Map Importer 自动重排通道。
- 完整 `CalculateDetails` 使用 `PdxTex2DGrad` 和默认 Detail UV 的导数选择 Mipmap，再用各材质独立 UV 采样。

`settings.terrain` 确认：

- `detail_blend_range = 0.25`
- `detail_tile_factor = 337.5`
- `detail_tile_offset_x = 0`
- `detail_tile_offset_y = -512`

2026-07-15 对本体 `ck3.exe` 中 `pdx_terrain.cpp` 的定点反汇编补齐了这些标量进入 `PdxTerrainConstants` 的 CPU 打包公式：

- `Terrain.XExtent` 与 `Terrain.ZExtent` 对应本作 `00_defines.txt` 的 `WORLD_EXTENTS_X = 9215`、`WORLD_EXTENTS_Z = 4607`。
- 默认 `DetailTileFactor` 和每个 `PackedDetailTileFactors[i]` 都按 `(tile_factor / Terrain.XExtent, -tile_factor / Terrain.XExtent)` 上传；Y 分量由原版 CPU 明确取负，不能在 Unity 中按普通正向 UV 猜测。
- `WorldSpaceToTerrain0To1 = (1 / XExtent, 1 / ZExtent)`，用于 colormap 等覆盖完整端点范围的地图纹理。
- `WorldSpaceToDetail` 先按 `((DetailTextureSize - 1) / DetailTextureSize) * WorldSpaceToTerrain0To1` 计算。由于 CK3 控制图为 9216×4608、世界端点为 9215×4607，结果精确化简为 `(1 / 9216, 1 / 4608)`。
- `DetailTexelSize = (1 / 9216, 1 / 4608)`，`DetailTextureSize = (9216, 4608)`；Shader 再执行 floor、半 texel 偏移和四邻 texel 聚合。

原版 `detail_index.tga` 与 `detail_intensity.tga` 头部进一步确认二者均为未压缩 32-bit BGRA、左下原点、8-bit Alpha，文件尾保留标准 TRUEVISION 页脚。Unity 的持久化控制图必须保持 BGRA 通道逻辑、Point/Clamp、无 Mipmap 和线性数值采样。

### 5.3 宏观颜色、光照和动态覆盖

地形最终颜色不是单纯的四层贴图混合：

- `colormap.dds` 提供大尺度色调。
- `pdxterrain.shader` 使用 `SoftLight` 将细节 diffuse 与 colormap 合成，强度还受材质通道影响。
- 材质属性进入 CK3/Jomini 地图光照，而非 Unity 默认 Lit 的简单替代。
- Shader 支持 Flat Map/Paper Map 过渡，并在顶点阶段把地形高度向 `FlatMapHeight` 插值。
- `province_effects.fxh` 和 `dynamic_masks.fxh` 处理雪、干旱、洪水、夏季草地等额外动态状态。
- `materials.settings` 开头明确登记 `drought`、`drought_cracks`、`flood`、`summer_grass`、`winter_effect` 动态材质。

雪属于动态材质覆盖的一部分，不能仅用“高度超过阈值就变白”替代。动态状态的 CPU 数据生成与季节推进若无直接证据，必须继续停留在调查阶段。

## 6. 省份、行政区划与政治状态

### 6.1 基础省份

证据文件：

- `game/map_data/provinces.png`
- `game/map_data/definition.csv`
- `game/map_data/default.map`
- `game/map_data/adjacencies.csv`

`provinces.png` 是 RGB 省份 ID 图；`definition.csv` 将数值省份 ID 映射到 RGB 和名称。`default.map` 另外登记海区、河流省份、湖泊、不可通行山地与不可通行海域。

不得把海区、湖泊、河流省份和普通陆地省份仅靠颜色亮度推断，必须使用 `default.map` 的原始分类。

### 6.2 法理行政层级

证据目录：`game/common/landed_titles/*.txt`。

标题层级使用：

- `e_`：帝国。
- `k_`：王国。
- `d_`：公国。
- `c_`：伯爵领。
- `b_`：男爵领/地产。

文件采用嵌套结构表达法理上下级；底层 `b_` 条目可以直接通过 `province = 数字ID` 关联地图省份。标题条目还包含 `color`、`capital` 等数据。

法理层级与开局后的实际统治领地不是同一个概念。实际领主、战争、继承和地图模式颜色属于运行时游戏状态，不能把 `landed_titles` 的静态嵌套误当成所有政治地图状态。

当前未确认 CK3 引擎如何把动态角色/头衔状态上传到颜色间接纹理；Unity 复刻该动态更新前必须继续寻找证据。

## 7. 政治色块填充与边缘渐变

证据文件：

- `jomini/gfx/FX/jomini/jomini_province_overlays.fxh`
- `jomini/gfx/FX/jomini/gradient_border_constants.fxh`
- `game/gfx/FX/bordercolor.fxh`
- `game/gfx/FX/pdxterrain.shader`
- `game/gfx/FX/pdxwater.shader`
- `jomini/gfx/FX/jomini/jomini_unsigned_distancefield.shader`

已确认结论：

- 政治填色与几何边界带是两个独立系统。
- `ProvinceColorIndirectionTexture` 和 `ProvinceColorTexture` 使用 Point 过滤承载省份到动态颜色的映射。
- `BorderDistanceFieldTexture` 使用 Linear 过滤。
- 必须采用 CK3 的游戏覆盖版而不是 Jomini 通用默认版：`game/gfx/FX/jomini/jomini_province_overlays.fxh` 启用 `BORDER_DISTANCE_FIELD_SAMPLES_MEDIUM`，实际为中心点加四个对角点共五次采样；对角偏移为 `0.75 * InvGradientTextureSize`，最后除以 5。Jomini 基础文件启用的九点 High 版本不代表 CK3 当前配置。
- `CalcPrimaryProvinceOverlay` 使用边界距离决定区域内部、外部、渐变和边缘颜色。
- 可调原版常量包括 `GB_GradientAlphaInside`、`GB_GradientAlphaOutside`、`GB_GradientWidth`、`GB_GradientColorMul`、`GB_EdgeWidth`、`GB_EdgeSmoothness`、`GB_EdgeAlpha`、`GB_EdgeColorMul`。
- 覆盖颜色分成 Pre-Lighting 和 Post-Lighting 两次混合，因此原版色块仍保留地形明暗，同时可以在光照后加强边缘颜色。
- 同一套政治覆盖会进入 Terrain 与 Water Shader；水面靠近陆岸时使用准确高度抑制重复覆盖。

因此原版“边界附近出现与本国色相一致的更深渐变”来自省份覆盖距离场和 pre/post lighting 混合，不是用粗黑线代替。

禁止使用少量邻点比较省份 ID 来冒充原版距离场。

全根目录复查已经找到距离场 GPU 生成器：`jomini_unsigned_distancefield.shader`。

该 Shader 直接确认：

- 距离场相对 Province Color 数据按 X/Y 各缩小 4 倍。
- `Init` Pass 在每个距离场 texel 周围检查 8×8 原省份颜色样本并写入最近异色边界的 Delta Vector。
- 可选 Wildcard Pass 可以屏蔽指定颜色组合。
- `Fill` Pass 从当前点和八邻域 Delta Vector 中保留平方距离最短者。
- `Finalize` Pass 输出 Delta Vector 长度除以 `MaxSearchDist` 的无符号距离。
- 全部 Pass 关闭 Blend 与 Depth。

`ck3.exe` 进一步确认 CPU 实现文件为 `jomini_gradient_borders.cpp`，原版系统名为 `GradientBorders`，并暴露 `FullRebuild`、`Enable`、`WildCardEnabled`、`WildCardSampleWidth`、`AlwaysRestart`、`IterationsPerFrame`、`WildCardSamples`、`MaxDistance`、`AlwaysDirty`、`UseIncreasingSampleWidth` 和 Jump Flood 调试入口。这确认了 GPU Shader 外确有增量矩形更新与迭代调度器。

尚未确认的是 `SampleOffset` 的完整数值序列、Render Texture 精确格式、上述调试/设置项的发行版默认数值和动态更新时机。实现调度器前必须继续追踪这些参数，不能仅凭常见 Jump Flood 算法补猜。

## 8. 几何地图边界

证据文件：

- `game/gfx/map/borders/settings.txt`
- `game/gfx/map/borders/*.dds`
- `game/gfx/FX/pdxborder.shader`
- `jomini/gfx/FX/jomini/jomini_flat_border.fxh`
- `ck3.exe` 中保留的 `jomini_border_generation.cpp`、`border_segment_extractor.cpp`、`jomini_border_manager.cpp` 与 `jomini_border_interface.cpp` 证据字符串

已确认结论：

- `pdxborder.shader` 接收已经生成的 `Position` 和 `UV`，说明 Shader 只绘制边界几何，不负责从省份图检测边界。
- 不同边界类型使用不同 DDS 笔刷和不同缩放范围。
- 已登记的层包括 water、province、county、domain、other_realm、my_realm、sub_realm、impassable、selected、highlight、war 等。
- 例如 province、county、domain、other_realm、sub_realm、impassable 的可见 zoom 区间并不相同，不能只按“公国/王国两层线宽”概括原版。
- 顶点阶段应用 `_HeightOffset`，并支持 Flat Map 高度过渡。
- 边界 Pass 使用 Alpha Blend、开启深度测试、关闭深度写入，并配置 `DepthBias = -30000`、`SlopeScaleDepthBias = -2`。
- 使用模板测试，函数为 `not_equal`、参考值为 1。
- 边界纹理决定横截面的颜色、透明度和笔触，不应替换成纯色程序线。
- 原版确实存在独立 CPU `GenerateBorders` 和 `ExtractSegments` 阶段；提取器遇到不连续省份图时会报告 `Border search failed to find next border pixel (corrupt province image?)`。
- 原版边界由 `CBorderManager` 管理，支持 `AddStaticBorders`、`UpdateBorders`、`SyncBorders`、`RemoveAllStaticBorders` 等生命周期，不是在 Terrain Shader 内比较相邻 ID 临时画线。

尚未确认且禁止推测：

- 从省份/头衔/统治状态提取连续边界拓扑的算法。
- 折线连接、分叉、闭环、端点、接头、UV 累积和简化规则。
- 原版边界 Mesh 的准确顶点布局。
- 无主地与国家、不可通行区与国家之间具体选择哪一种边界类型的 CPU 判定。

这些逻辑位于现有 Shader 之前。现在已经定位原版生成器所在的编译单元，但函数正文未随游戏公开；在恢复足够字段和算法证据前，不能用自制线段 Mesh 声称复刻了 CK3，也不能通过扩大端帽、删除短段或重叠三角形修补断线。

## 9. 河流

证据文件：

- `game/map_data/rivers.png`
- `game/common/defines/jomini/rivers.txt`
- `game/gfx/map/rivers/rivers.settings`
- `game/gfx/map/rivers/riverwater.settings`
- `game/gfx/FX/river_surface.shader`
- `game/gfx/FX/river_bottom.shader`
- `jomini/gfx/FX/jomini/jomini_river.fxh`
- `jomini/gfx/FX/jomini/jomini_river_surface.fxh`
- `jomini/gfx/FX/jomini/jomini_river_bottom.fxh`

### 9.1 源图与宽度

已确认的 `rivers.png` 编码类别：

- 白色：陆地背景。
- 洋红色：海洋背景。
- 蓝色/青色系列：河流宽度等级。
- 红色/绿色像素：河网控制标记，不是可见河水。

`NRivers` 直接确认：

- `NUM_WIDTH_PIXEL_VALUES = 13`
- `WIDTH_MIN = 1.0`
- `WIDTH_MAX = 4.0`
- CK3 覆盖配置使用 `FADE_IN_DISTANCE = 10.0`、`FADE_OUT_DISTANCE = 5.0`、`UV_SCALE = 0.8`

因此所有河流同宽不符合原版输入；海洋中的洋红背景也绝对不能当作河流绘制。

### 9.2 原版河流几何和渲染

`VS_INPUT_RIVER` 证明原版河流是带宽度的预生成几何，顶点包含：Position、Transparency、UV、Tangent、Normal、Width、DistanceToMain。

河流至少分为两个渲染组成：

- `river_surface.shader`：调用与海洋共享的水体计算，使用流动法线、泡沫、透明度、边缘淡出、阴影、云影和雾。
- `river_bottom.shader`：使用河床 diffuse/normal/properties，计算横截面深度、视差和河床光照。

河流横向深度根据 `UV.y` 形成从岸边到中心的曲线；表面 Alpha 还使用 `Transparency`、`DistanceToMain` 和两岸 `smoothstep` 淡出。河流进入远景/平面地图时会淡出。

### 9.3 当前不能确认的部分

二进制复查已经确认该部分不是“没有原版实现”，而是编译进 `ck3.exe` 的 Jomini CPU 系统：

- `jomini_river_graphics.cpp` 与 `CRiverInitTask` 负责位图河流初始化/绘制。
- `maprivers.cpp` 包含 `GenerateRiverData`、`CalculateRiversByProvince`，并暴露 `Rivers.SmoothIterations`、`SmoothKernelSize`、`TessellationMinDistance`、`TessellationMaxAngle`、`SmoothFadeDistance`。
- 原版会验证 river bitmap 与 height map 分辨率相同，并显式处理 `main river source`、`secondary river source`、父河流和未知河流类型。
- 本体还包含另一套 Spline River/Network 编辑与渲染系统，但当前 `game/gfx/map/spline_network` 没有 CK3 世界河网资产；不能把通用 Spline River 工具误认成 CK3 的 `rivers.png` 最终管线。

2026-07-17 已通过当前 `ck3.exe` 的目标函数定点反汇编恢复位图解析与中心线阶段。解析器入口为 `0x1430445F0–0x1430450B0`，四邻域枚举为 `0x143043C40`，中心线追踪为 `0x143043D00–0x1430444E4`，父河查找为 `0x1430444F0`。已确认：索引 `0` 是主河源，`1/2` 是需要相邻已归属父河点的次级源；普通路径只接受索引 `3..253`；邻域顺序严格为左、右、上、下；主河先建，次级源按原扫描顺序分批建立；无进展时忽略仍未连接的源点。当前原图会留下 3 个未连接的索引 `1` 标记，这是原版解析流程的输入结果，不得改成报错或自行补线。

宽度函数 `0x14301DBC0` 已确认索引 `<3` 使用 `0.01`，其余索引使用原版 Defines 的 13 级线性映射。路径浮点化函数 `0x14301E5A0–0x14301ED96` 已确认首尾、内部二点平均和支流父河方向调整。`GenerateRiverData` 位于 `0x14301EDA0–0x14301FAE3`，最终 56 字节顶点构建位于 `0x14301DEF0–0x14301E1A0`。通用平滑/细分函数和分支端点的剩余参数仍需闭合；在此之前可以持久化已确认的河网拓扑资产，但不得自行设计最终河面 Mesh。

## 10. 海洋与湖泊

证据文件：

- `game/common/defines/00_defines.txt`
- `game/map_data/default.map`
- `game/gfx/map/water/water.settings`
- `game/gfx/map/water/*`
- `game/gfx/FX/pdxwater.shader`
- `game/gfx/FX/jomini/jomini_water_default.fxh`
- `jomini/gfx/FX/jomini/jomini_water.fxh`
- `jomini/gfx/FX/jomini/jomini_water_pdxmesh.fxh`

已确认结论：

- `NJominiMap` 明确给出 `WORLD_EXTENTS_X = 9215`、`WORLD_EXTENTS_Y = 50`、`WORLD_EXTENTS_Z = 4607` 和 `WATERLEVEL = 3`；基础海面必须使用该水位与完整世界范围。
- 对 `ck3.exe` 中 `jomini_water.cpp` 的定点反汇编已恢复普通海洋几何写入：CPU 只写四个 `int2` 角点，顺序为 `(xmin,ymin)`、`(xmin,ymax)`、`(xmax,ymin)`、`(xmax,ymax)`，以覆盖完整世界的三角形条带提交。Unity 不支持 Triangle Strip MeshTopology 时，可以把同一四角平面展开为两个三角形；这只改变索引承载形式，不改变原版几何。
- 普通海洋顶点把输入 X/Y 映射到世界 X/Z，并把世界 Y 固定为 `_WaterHeight`，即基础海面是恒定高度平面。
- 海水深度通过 `_WaterHeight - terrain height` 获得，海岸淡出、透视、折射和泡沫都依赖真实地形深度，而不是省份颜色边缘。
- `watercolor_rgb_waterspec_a.dds` 的 RGB 提供空间水色，Alpha 提供高光输入。
- 三层不同缩放、旋转、速度的 ambient normal 与 flow normal 合成水面法线。
- 水色在 shallow/deep 之间根据观察方向混合，并使用 Fresnel、反射、折射、可见水下地形和雾。
- `foam_map.dds`、`foam.dds`、`foam_noise.dds`、`foam_ramp.dds` 共同产生泡沫；泡沫受深度海岸遮罩和流动遮罩影响。
- CK3 的游戏覆盖版本还实现向岸推进的波线，并通过 `foam_map` 的绿色通道限制。
- Water Pass 使用 Alpha Blend，Rasterizer `DepthBias = -100`。
- CK3 原生 Rasterizer 的整数 `DepthBias=-100` 与 ShaderLab Offset 没有已确认的一比一数值换算。`Offset 0,+100` 已由实际验收否定。Unity 2022.3 官方文档确认 `units` 负责恒定偏移、`factor` 负责随视角变化的 Z 斜率，并用 `Offset -1,-1` 处理共面冲突；该 Offset 只能作为 Unity 载体值，不能描述成 CK3 原版参数。
- 2026-07-17 的 Scene/Game 验收进一步证明：仅使用 `Offset -1,-1` 时，恒定 `WATERLEVEL=3` 的 Unity 水面会在地形进入 `FLAT_MAP_HEIGHT=3.92` 后被整张地形深度压住，只随镜头角度和深度精度偶发闪现；透明四角平面写深度还会造成 Scene 视图的整面截断。当前 Unity 载体因此在 `_FlatMapLerp` 期间把渲染用水面高度同步过渡到 `_FlatMapHeight + 0.02`，并关闭透明水面深度写入；真实水深仍严格以原版 `_WaterHeight=3` 采样计算。`0.02` 是可调 Unity 深度载体补偿，不是 CK3 原版数值。
- `pdxwater.shader` 有独立 `water`、`waterLowSpec`、`lake` 和 `lake_mapobject` Effect。
- `default.map` 明确列出 `sea_zones` 和 `lakes`，不能仅靠水面高度猜测水域分类。

此前“普通海洋平面网格 CPU 方式未确认”的结论已经作废：原版普通海洋是上述完整世界四角平面，不存在需要自行设计的海洋分块拓扑。仍未恢复的是高于基础海面的独立湖泊 Mesh 生成方式；湖泊部分继续受证据门禁约束，不能自行轮廓追踪替代。

## 11. 地图标签

证据文件：

- `game/common/defines/graphic/00_graphics.txt`
- `game/fonts/fonts.font`
- `Paradox_King_Script` 字体资产
- `jomini/gfx/FX/jomini/countrynames.fxh`
- `game/gfx/FX/mapname.shader`
- `game/gfx/map/map_modes/map_modes.txt`

可以确认 CK3 使用的字体资源以及 `NMapName` 中部分地图名缩放参数。`countrynames.fxh` 还确认标签输入是带世界 Position 和 TexCoord 的预生成几何；顶点阶段支持 Flat Map 高度插值。`mapname.shader` 确认字体 Atlas 的距离场平滑、内部/描边、噪声覆盖、战争迷雾/云影颜色、Alpha Blend 以及“不等于 1”的模板遮挡。`map_modes.txt` 明确规定不同地图模式使用 realms、baronies、counties、dejure_duchies、dejure_kingdoms、cultures、religions 等不同 small/large map name 数据源。

`ck3.exe` 已定位 Jomini 侧 `countryname.cpp`，其中出现 `Name._Vertices`、`No points for area name`、`Failed getting text data for country name`；CK3 游戏侧 `mapgraphics.cpp` 则包含 `UpdateMapNameData`、`UpdateMapNameMode`、`UpdateNamesIfNeeded`、`FillBaronyNames`。这确认名称几何由 CPU 预生成并按地图模式/可见省份更新，而不是单个水平 TextMesh。

2026-07-16 对 `ck3.exe` 的名称 CPU 正文继续定点恢复：

- Jomini 主布局函数位于 `0x142fb2070–0x142fb3d8f`。输入不是矩形，而是一组组成同一名称区域的省份对象；函数直接读取每省的像素跨度、像素数和边界，并把所有属于该区域的像素合并参与计算。
- 函数以 `COUNTRY_NAMES_STRIDE_SIZE` 为初始步长，并根据区域总像素数自适应调整，使采样量不超过 `65536`；随后累计 `x`、`y`、`x*y`、`x²`、`y²`、样本数与整体边界，求区域中心和两种主轴回归方向。
- 每种方向都以中心线为基准，按 `COUNTRY_NAMES_NUM_LINE_TESTS` 的正负范围和 `COUNTRY_NAMES_TEST_LINES_SPACING` 生成平行候选线；候选线与由区域省份构成的多边形求交，只保留区域内部的连续线段，并为每个方向选择最长线段。
- 两个方向最终按线段长度平方比较，其中更水平的方向先乘 `COUNTRY_NAMES_HORIZONTAL_BIAS = 1.6`，这正是原版偏好横排但仍允许狭长区域竖排的来源。之后才读取 `MapFont` 字形度量，应用 `COUNTRY_NAMES_SCALE`、宽高留白上限、最大字符间拉伸、方形区域阈值和短名称曲线截止值。
- `0x142fb1d10–0x142fb2061` 会按字体图集/材质引用归并已经生成的字形顶点，`0x142fb0f10` 接收选中线段、区域边界和文本度量生成最终曲线字形数据。最终每个标签保存顶点、包围盒、缩放与方向状态，而不是保存一个中心点加旋转角。

同日继续恢复 CK3 游戏层名称模式与 Jomini 曲线阶段，新增确认：

- 原版二进制的连续脚本符号表 `0x1442c06d0–0x1442c078f` 依次登记 `baronies`、`counties`、`duchies`、`kingdoms`、`empires`、`dejure_counties`、`dejure_duchies`、`dejure_kingdoms`、`dejure_empires`、`realms`、`cultures`、`realm_areas`。这与 `UpdateMapNameData` 的 `0–11` 分派顺序逐项吻合，因此实际国家名称必须走 `realms = 9`，不能拿法理王国名称代替。
- `realms = 9` 在 `0x140b107b0` 的分派中选择 `0x140b1e0e0` 与 `0x140b1e120` 两个专用回调。通用收集器 `0x140b1db80` 遍历地图实体，第一个回调取得用于合并区域的稳定节点编号，第二个回调取得该节点的原版本地化显示名；随后才把属于同一节点的省份送入 Jomini 名称几何生成器。
- 两个 Realm 回调都调用 `0x140b00f00`，选择器固定为 `5`。该函数从当前地图实体开始，使用 `0x140967620` 读取父节点编号字段并逐级解析父节点，只有最终节点类型字段与选择器相等才返回。Realm 的合并键直接取返回节点 `+0x10`，显示名来自同一节点的本地化名称对象，而不是颜色值、包围盒名称或法理头衔名。
- 通用收集器之后的 `0x140b20ed0` 还会在地图实体的邻接表（实体内偏移 `+0x40`）上执行显式洪泛遍历。只有“与当前实体相邻，并且 Realm 合并键相同”的实体才进入同一组；因此同一 Realm 的不相连飞地或隔海部分会形成独立名称候选，原版不会用一个标签跨越所有不相连领土。
- `0x142fb04c0–0x142fb0f0e` 已确认会根据选中的区域内线段、实际多边形交点与按六项分组的采样数据生成五个二维曲线控制点；`0x142fb0f10` 随后按三次曲线位置及其一阶导数得到每个字形的切线和法线，把字形四角逐个变换到曲线上，并继续执行地图高度采样。原版标签因此同时具有弯曲、逐字朝向和贴图高度，不能用一个整体旋转的文本对象等价替代。
- 脚本接口 `GetPrimaryTitle` 已由字符串注册点追到 `0x1425f3350`。该函数本身不比较头衔等级：存在对象 `+0x1b8` 时，它检查其中 `+0x1ec` 的数量并直接读取 `+0x1e0` 列表的第一个头衔编号；否则从对象 `+0x1c8` 指向的备用结构读取 `+0x74` 数量和 `+0x68` 列表首项。随后只负责通过 landed-title 对象池解析该编号。这证明主头衔是引擎预先维护的有序列表首项，而不是地图名称阶段临时按最高等级猜选。
- 原版角色历史确实允许用 `set_primary_title_to = title:...` 明确改变该顺序；当前 `game/history/characters` 中存在这类记录。没有显式记录的角色仍依赖引擎在授予、失去和继承头衔时维护列表的规则，不能用文件名、颜色或字母顺序替代。

2026-07-16 对 `0x142fb0f10–0x142fb1bb0` 的逐指令检查又确认：原版没有把字符按曲线弧长重新平均分配。字体阶段先输出通常的水平排版，每个字形为连续六个、步长 `0x1c` 的顶点；曲线阶段以字形原始 X 中心在整行水平跨度中的归一化位置作为曲线参数，使用三次曲线位置和一阶导数构造切线/法线，再变换该字形六个顶点并逐顶点调用地形高度采样。因此“弧长分配未知”不再是门禁，Unity 必须保留原字体水平 advance，不能自行均匀排字。

当前尚未闭合无显式 `set_primary_title_to` 时头衔列表的构建/重排顺序，以及 CK3 如何在游戏进行中从角色、主头衔与领主状态动态重建 Realm 父节点图。因此不能把静态书签快照声称为完整运行时 `realms=9`。但当前 F2 本身就是 `1066.9.15` 静态预览：其填色已直接使用原版 title history 的 `holder` 与显式 `liege` 链解析顶级领地，所以 F2 名称必须复用同一分组，并读取顶级头衔的 `historical_name`/本地化名称，不能再拿颜色反猜或复用法理名称。

因此不得通过区域包围盒、最长轴或手工旋转声称复刻了 CK3 标签布局。法理 `dejure_kingdoms=7` 按省份实体邻接连通域持久化名称源；F2 的 `1066.9.15` 静态实际领地名称按其政治填色已经采用的显式头衔历史链持久化。两者都交给同一个已确认的回归轴、十一条平行测试线、最长连续段、五控制点和逐字曲线 Mesh 算法。动态日期与玩家改变领主关系后的实时 Realm 重建仍需等运行时 Realm 节点链完全闭合。

Unity 阶段 8 的第一步已可重复构建：它从完整 `provinces.png` 扫描实体接触关系、叠加 `adjacencies.csv`，仅对相同 `dejure_kingdoms=7` 键执行连通域合并，并保存英文原版本地化名称与 `Paradox_King_Script.otf` 的 50 点 SDF 图集。

2026-07-16 首次曲线 Mesh 预览经用户验收判定与原版明显不一致。复核发现该版本错误地把五个区域采样点用最小二乘拟合成单段 Bezier，并自行推导了缩放与小区域拒绝条件；这些都没有 CK3 证据，现已停用，不能作为后续调参基础。继续反汇编 `0x142fb0f10` 已确认原版把五个点分为前后两段，每段取相邻四点并使用标准 Catmull-Rom 三次基函数；参数先限制到 `[0,1]`，以 `0.5` 分段后把段内参数乘二。位置权重依次为 `0.5*(-u³+2u²-u)`、`0.5*(3u³-5u²+2)`、`0.5*(-3u³+4u²+u)`、`0.5*(u³-u²)`，导数也由同一基函数直接计算。必须继续闭合 `0x142fb04c0` 的五点输入构造和主布局末段的精确缩放/拒绝条件，再重新开放持久 Mesh 构建。

同日对 `0x142fb3300–0x142fb3be6` 和原版 Defines 注册函数的交叉复核继续确认：`0x14570f6c4`、`0x14570f6d4`、`0x14570f6d8`、`0x14570f6c0`、`0x14570f6d0`、`0x14570f6dc`、`0x14570f6bc` 分别就是 `COUNTRY_NAMES_SCALE`、`SCALE_CAP_WIDTH`、`SCALE_CAP_HEIGHT`、`MAX_STRETCH_FACTOR`、`HORIZONTAL_BIAS`、`SQUARENESS_THRESHOLD` 与 `SQUARENESS_THRESHOLD_SINGLE_CHAR`。原版先以选中候选线长度和字体文本宽度计算宽度适配比例，再以区域采样面积除以候选线长度得到区域平均可用厚度，并结合字体文本高度计算高度适配比例；宽高比例接近方形阈值时会改用高度适配比例，同时只在字符之间增加间距，间距拉伸受 `MAX_STRETCH_FACTOR` 限制，最终比例再乘 `COUNTRY_NAMES_SCALE`。这不是旧实现的 `min(曲线近似长度, 自制 minorSpan)` 公式。

`0x142fb3717–0x142fb3b7e` 进一步确认传给五点函数的中间数组每项固定为 20 字节，布局阶段严格为每条区域截线写入六项；`0x142fb04c0` 按六项为一组重新求与真实区域边界的交点，分别保留首组、末组和中间组平均结果，再通过相邻方向的归一化与二维线交点构造最终五个控制点。旧 `CK3MapNameMeshBuilder` 中的 PCA、固定 2 像素扫描、`minorSpan`、单段 Bezier 和经验拒绝逻辑已从代码中完整移除；场景中先前生成的旧名称对象不属于验收结果，应保持隐藏。

2026-07-16 的后续逐指令复核补齐了上述记录和区域截线链：20 字节记录就是 `float3 Position + float2 TexCoord` 的名称顶点，六项对应一个字形的两个三角形，不是另一个区域轮廓结构。`0x143bbbdd0` 是严格整数 Bresenham 线栅格器；`0x142fafd70` 沿该像素线读取 16-bit Province ID，映射到省份对象并按当前 Area 的省份集合/原版省份标志裁出连续有效段，优先保留穿过目标位置的段，否则按到目标位置的平方距离选最近段。`0x142fb04c0` 对每组六顶点取字形横向位置，在已选主线的法线方向重新栅格化区域截面；首尾截面把主线位置与区域截面中心各取一半，中间点为全部截面中心平均，随后以相邻方向的单位向量、点积和二维叉积外推首尾两个切向控制点，最终输出 `外推点、首截面、中间平均、末截面、外推点` 五点。`0x142fb0f10` 再把可见曲线从第二点到第四点分成两段 Catmull-Rom。

同一轮还补齐了原版采样步长和主轴公式：步长从 `COUNTRY_NAMES_STRIDE_SIZE=4` 开始；若 `stride² > 区域总像素数` 就持续减半至 1，随后只要 `区域总像素数 / stride² > 65536` 就持续翻倍。采样严格累计 `x、y、x*y、x²、y²、样本数`，两条候选轴分别使用 `y = cov/varX*x + intercept` 与 `x = cov/varY*y + intercept`，不是 PCA 特征向量。每条轴按 `COUNTRY_NAMES_NUM_LINE_TESTS=5` 实际测试 `-5…+5` 共十一条平行线，间距为 `10`；一条测试线被 Area 孔洞切开时，先按 `0x142fafd70` 选择穿过区域目标点或离目标点最近的连续段，再在十一条测试线的结果中选最长段。第一条 X 主轴的长度平方乘 `COUNTRY_NAMES_HORIZONTAL_BIAS=1.6` 后再与 Y 主轴比较。

`CK3MapNameMeshBuilder` 已按这些闭合规则恢复为单一 Editor 构建入口，输出持久 Mesh、R8 字体距离场和材质，并在 Scene/Game 绑定同一资产。名称像素 Shader 已移植证据完整的 Flat Map 像素链：R 通道 SDF、`TEXT_WIDTH=0.05`、原版 LOD/字重常量、`rough_texture_overlay.dds` 的 `(20,20)/(50,30)` 采样、`0.2–0.8` 描边噪声、`0.4/2.5/0.2` 描边常量、平面地图文字/描边色与 `0.5` Overlay Blend。近景暂时复用这套已确认的字形/颜色结果，不再整段强制透明；近景 FoW、云影和距离雾仍未接入，不能把当前近景观感声称为最终原版效果。

2026-07-17 全世界远景验收进一步修正了名称布局载体的三处偏差：区域回归轴仍使用原版 stride 采样，但字号高度适配改回 Area 的精确 `PixelCount`，不再用 `stride²×采样数` 近似；方形区域阈值改为比较宽度适配比例与高度适配比例，不再错误比较文字自身宽高；字体水平跨度和居中改为实际字形四边形的最小/最大 X，保留字体 advance 负责逐字排布。这三项直接对应 `0x142fb2070–0x142fb3d8f` 的独立区域像素数输入、`0x142fb3300–0x142fb36e9` 的宽高适配判断，以及 `0x142fb0f10–0x142fb1bb0` 的六顶点水平跨度输入；没有加入头衔专属偏移或字号例外。

2026-07-17 后续全世界名称验收发现拜占庭名称仍偏向安纳托利亚。定点数据确认其 Area 均值中心约为 `(2648, 2413)`，旧 Unity 载体却在同一测试线上无条件选择更长的右侧断片，主线中点落到约 `(2977, 2385)`。这不是字体居中问题，而是遗漏了 `0x142fafd70` 的目标位置段选择。`CK3MapNameMeshBuilder` 现以原版回归采样均值作为区域目标点：每条测试线优先选择穿过该点的连续段，否则选择平方距离最近的段；十一条候选线之间仍按原版比较长度。未加入拜占庭或宋的专属位置、字号倍率。

2026-07-18 的第二次全世界名称验收确认字号仍有系统性过大、过小和曲线偏心。代码审计发现两处与已登记二进制证据直接冲突的 Unity 移植错误：`0x142fb3300–0x142fb36e9` 使用 `Area / 主线长度` 得到平均可用厚度，旧代码却额外乘了 `2`，会把高度受限名称最多放大一倍；`0x142fb04c0` 的首末截面使用首末字形中心所在主线点与真实截面中心各取一半，旧代码却错误使用整行文字的左右边缘。当前构建器已删除错误倍数，并用首末字形的实际 base point 构造首末控制点。该修复没有加入统一字号上下限、头衔特例或人工偏移；缩放 LOD/名称密度仍是单独未完成模块。

2026-07-20 的跨海政体名称复核补齐了 `0x142fafd70` 的省份标志条件及直接调用链 `0x142fb3e10 → 0x142fb2070 → 0x142fb0f10/0x142fb04c0 → 0x142fafd70`。原版区域像素统计只使用 Area 省份位集；候选主线和字形法向截线则接受 `Area 省份 OR 大河省份 OR（Area 水域选项开启时的海区/湖泊）`，因此爱琴海不会把拜占庭的名称候选线强制切成欧洲侧和安纳托利亚侧两段。省份对象 `+0x1b` 已由不可通行归色调用链确认为普通可通行陆地，`+0x1c` 为不可通行；剩余三项水域判定与 `default.map` 的 `river_provinces / sea_zones / lakes` 闭合。Unity 端现把 `RegionMask` 分成 `ContainsArea` 与 `ContainsLine`：海区、湖泊和大河只参与截线连通，绝不进入面积、均值中心和字号计算；没有扩大政体领土，也没有加入政体专属偏移。

地图名称的顶点高度必须与地形共用同一套平面地图过渡。原版 `jomini/gfx/FX/jomini/countrynames.fxh` 执行 `WorldSpacePos.y = lerp(WorldSpacePos.y, FlattenTo, FlattenAmount)`，`pdxterrain.shader` 的 `TERRAIN_FLAT_MAP_LERP` 分支执行同式；`common/defines/graphic/00_graphics.txt` 确认 `FLAT_MAP_HEIGHT = 3.92`、`FLAT_MAP_ZOOM_STEP = 21`。Unity 端因此由 `CK3MapModeController` 在缩放档 20→21 同步驱动地形与名称的 `_FlatMapLerp`，并共用 `_FlatMapHeight = 3.92`，避免相机平移时名称与底图产生高度视差。

2026-07-17 的近景移动验收发现名称 Mesh 构建器残留了无原版证据的 `+0.35` 世界高度抬升，导致文字与地形之间产生可见透视视差。原版 `mapname.shader` 的名称 Pass 明确使用 `DepthEnable=no`，不需要靠抬高几何规避深度冲突；该自定义抬升已删除。名称顶点现在直接使用与 Terrain Shader 相同的原版高度页表采样，随后再与地形共同执行 Flat Map 高度过渡。

第一次删除常量后验收仍有视差，证明仅修正烘焙偏移不足。进一步审计发现旧名称 GameObject 被复用时没有强制重置局部 Transform，而且名称 Mesh 的 Y 仍来自 Editor CPU 烘焙函数，Terrain 的最终 Y 则来自 GPU Shader 页表函数。当前构建器会把 F1/F2 名称对象强制设为相同 Terrain 根节点下的 `localPosition=0`、`localRotation=identity`、`localScale=1`；`CK3MapName.shader` 也已直接绑定 Terrain 同一组高度间接纹理、压缩高度纹理、五级页常量和高度倍率，并复制 Terrain Shader 的同一个 `CK3GetHeight` 函数。烘焙 Mesh 的 Y 不再参与最终显示高度，因此相机移动时不存在第二套高度计算。

同一轮画面审计还确认旧 Unity 名称 Shader 也不是可保留的近似基础。原版 `game/gfx/FX/mapname.shader` 从字体 Atlas 的 R 通道读取距离场，固定 `TEXT_WIDTH=0.05`，使用 `COUNTRY_NAMES_LOD_FACTOR` 和 `THICKNESS_BIAS`，并采样 `game/gfx/map/textures/rough_texture_overlay.dds`：内部纹理坐标缩放为 `(20,20)`，描边缩放为 `(50,30)`，描边噪声在 `0.2–0.8` 间变化，最后以 `0.5` Overlay Blend 混合。近景分支还依次应用 FoW、云影与距离雾，平面地图分支使用独立的 `TEXT_COLOR_FLATMAP` 和 `OUTLINE_COLOR_FLATMAP`。旧 Unity Shader 省略这些链路并读取 TMP Atlas 的 Alpha 通道，造成截图中文字过黑、过硬且描边均匀；现已替换为全透明证据门禁 Shader，避免错误旧 Mesh 继续出现在 Scene/Game。只有原版字体图集通道与上述完整像素链闭合后才恢复可见渲染。

## 12. 当前架构结论

从现有证据可确认 CK3 地图是多套数据和渲染系统叠加，而不是一张政治贴图：

1. 节点化 LOD 高程 Terrain，带 Skirt 和平面地图高度过渡。
2. 四层索引/权重驱动的地表材质系统，叠加 colormap、动态环境材质和地图光照。
3. 省份 ID 与法理头衔层级提供行政基础，实际政治状态由运行时游戏状态决定。
4. 省份颜色间接纹理与边界距离场负责政治填色和同色边缘渐变。
5. 独立的预生成几何边界带负责具有笔刷纹理和层级缩放的边界线。
6. 河流是带属性的几何，并分河床和水面两个渲染组成。
7. 海洋是恒定水位表面，使用地形深度、流图、法线、折射、反射和泡沫图完成岸线效果。
8. 标签是独立系统，当前布局算法证据不足。

不能把这些系统合并成“在 Terrain Shader 里比较 ID 并画所有线”的单一方案。

## 13. 明确禁止的历史错误

- 用 Voronoi、随机分区或噪声替代 CK3 行政数据。
- 按高度自动猜测全部草地、沙地、雪山材质。
- 从截图猜 Shader。
- 用纯黑程序线替代 CK3 DDS 边界笔刷。
- 用相邻省份 ID 采样冒充 CK3 距离场。
- 删除短边界、扩大端帽或叠加三角形掩盖拓扑错误。
- 把无人区填给最近国家。
- 把河流图的海洋背景色当河流。
- 所有河流使用同一宽度。
- 用 `ZTest Always`、Overlay 或反复修改 Render Queue 掩盖贴地错误。
- Runtime 动态创建或克隆静态地图资产。
- 修复失败后继续叠加补丁，而不重新审计原版证据。

## 14. 验收标准

模块只有同时满足以下条件才算完成：

- 每个关键步骤都有 CK3 原版证据。
- 未确认的引擎内部步骤没有被自制算法冒充。
- 非 Play 模式可以完整预览。
- Scene 与 Game 使用同一套持久化资产。
- Inspector 参数中文、有效并对应原版含义。
- Runtime 不生成该模块的静态资产。
- Shader 无编译错误和紫色材质。
- 没有 Missing Reference、临时 Mesh 或未保存对象。
- 构建工具入口唯一、结果确定且可重复。
- 证据登记与代码同步。

## 15. 当前状态总览（唯一有效进度表，2026-07-17）

本节是判断项目进度和安排下一步工作的唯一入口。后文阶段记录用于保留证据和构建历史；如果其旧“状态”与本节冲突，以本节为准。

| 模块 | 当前状态 | 已有结果 | 下一动作 |
|---|---|---|---|
| 原版数据基线 | 已验收 | 3,022 个原版文件已同步并验证；包含 `game/common/defines/00_defines.txt` 的水位和世界范围 | 后续只在证据库缺项时增量补充 |
| Terrain 材质与 Map Lighting | 第一版已接入、待 Unity 验收 | 105 套材质、三组 DXT5 数组、四层索引/权重、colormap、完整世界控制图、原版材质通道、GGX 直射光和晴天地形 IBL | 后续补云影双场景、Shadow Tint 和动态季节覆盖 |
| Terrain 高度与 Mesh | 固定原版 LOD 预览完成、已显示 | 原版高度页表、33×33 节点 Mesh、Skirt、`nodes.dat` 完整世界固定层级场景 | 最终联调阶段再恢复自适应 Cull/LOD；当前保持全部可见 |
| 省份与法理数据 | 已完成 | 13,270 条定义、原版省份分类、特殊邻接、`h/e/k/d/c/b` 层级、中国 `h_china` 已纳入 | 随政治模式继续核对少数动态头衔状态 |
| F1 法理王国填色 | 已实现、已显示 | 完整世界颜色表、不可通行区域多数归色、距离场、渐变参数 | 水体完成后复验岸线和最终色彩观感 |
| F2 1066.9.15 实际领地填色 | 已实现、已显示 | title history holder/liege 链、动态中国颜色、实际领地颜色表 | 水体完成后复验；继续核对多顶级头衔的主头衔选择 |
| 政治边缘渐变 | 已实现、待水体联合验收 | CK3 UDF Pass、游戏覆盖版五点采样、Pre/Post Lighting 参数、含/不含水岸两套距离场 | 海洋与湖泊渲染完成后判断湖岸是否仍有错误深边 |
| F1/F2 曲线地图名称 | 基础版本已验收 | 原版字体、两套静态 Mesh、五控制点曲线、模式切换；名称与 Terrain 使用同一 GPU 高度页表函数，WASD 平移无高度视差 | 后续补近景 FoW/云影/距离雾、缩放 LOD、最终密度和动态 Realm 状态 |
| 战略相机与模式切换 | 可用 | F1/F2、WASD、滚轮缩放、原版 35 档距离/倾角输入；Unity 载体已增加可关闭、可调时间和最大速度的档位间平滑 | 继续核对原版输入到世界位移换算；不阻塞水体 |
| 海洋与湖泊 | 基础海面已验收；高级水体待实现 | 原版 `WATERLEVEL=3`、世界范围、四角全世界海面网格、BC7 水色图、`waterLowSpec` 高度/淡出 Shader；平面地图高度联动和 Unity 深度载体已稳定 | 继续恢复高位独立湖泊与近景水体：波浪、Flow、Fresnel、反射/折射、泡沫和推进波线 |
| 河流 | 原版拓扑数据阶段 10 与可视河面阶段 11 已接入统一构建器 | 0/1/2 源点、父河、13 级宽度、两轮平滑、Catmull-Rom、自适应角度细分、持久化河面 Mesh 与地形高度贴合 | Unity 阶段验收后继续接原版河底材质、流动法线、泡沫与入海淡出 |
| 几何边界带 | 未实现 | 原版笔刷、Pass 状态与生成器入口已定位 | 恢复连续拓扑、UV、接头和分叉后再做 Editor Mesh |
| 点击、悬停、选中高亮 | 未开始 | 原版边界/覆盖入口已有部分证据 | 在几何边界和水体稳定后实现 |
| 建筑、树木、山石 | 全世界树木与城堡实例构建已接入、待 Unity 验收 | 直接解码原版 pdxasset `.mesh` 的 `p/n/ta/u0/tri`；读取全部树木 generator 的 451,562 条 transform；城堡坐标由 `building_locators.txt` 和基础 `castle_holding` 交集生成 | 验收 Scene/Game 可见性、近景帧率、Mesh 朝向、纹理 Alpha、尺寸和贴地；再接原版 graphical culture/信仰/等级模型分派、tree.shader 完整移植和雪山覆盖 |
| 桌面、远景手绘图与世界外侧底图 | 阶段 13 已实现、待 Unity 验收 | 原版同步已加入默认西式桌体/桌布/蜡烛/道具及跨目录材质依赖；Editor 构建四组完整多子网格 Mesh 与原始 Transform；逐子网格 `.asset` 覆盖、缺图校验、桌面 GGX/室内 Cubemap 光照已接入；Terrain/Water 共用 9216×4608 Flat Map、20→21 档压平和撕纸过渡；Surround flat pass 已接入 | 在 Unity 重新同步原版数据并执行阶段 13；静态链验收后继续桌布动画、蜡烛火焰和动态 Surround 云/阴影 |

当前整体状态：完整世界数据、Terrain 内核、F1/F2 政治覆盖、同色边缘渐变、曲线名称、战略相机和基础海面已经形成可观看版本；CK3 Map Lighting 第一版已进入 Unity 验收。主要缺口是高级水体、河流、原版几何边界带、交互高亮、自适应 LOD/Cull，以及 Map Lighting 的云影双场景和 Shadow Tint。

### 15.1 近景装饰全世界实例阶段（2026-07-17）

- A 级模型证据：`game/gfx/models/mapitems/trees/*.mesh/.asset`、`game/gfx/models/mapitems/cliffs/*.mesh/.asset` 与 `game/gfx/models/buildings/holdings/*.mesh/.asset`。CK3 的二进制 `pdxasset` Mesh 直接保存位置 `p`、法线 `n`、切线 `ta`、UV `u0`、三角形 `tri` 以及材质纹理名；当前 Editor 解码器只取 LOD0/首个主体子网格，输出持久 Unity Mesh。
- A 级树木摆放证据：`game/gfx/map/map_object_data/generated/tree*generator*.txt` 明确保存 `pdxmesh`、`count` 和每实例 10 个浮点数的 position/quaternion/scale。构建器现会发现全部 16 个原版树木生成文件，保存约 45 万条完整世界实例，不创建逐实例 GameObject。
- A 级城堡位置/类型输入：`game/gfx/map/map_object_data/building_locators.txt` 保存以省份 ID 为键的 position/quaternion/scale，`game/history/provinces/*.txt` 保存基础 `holding = castle_holding`。当前构建器取两者交集作为城堡实例；已导入的 `building_western_castle_01` 是载体预览原型，全世界 graphical culture、信仰、建筑等级与日期变化的模型分派尚未闭合，不得称为最终原版建筑外观。
- A 级山石原型证据：`game/gfx/map/map_object_data/cliffs_rock.txt` 列出 `cliff_big_01_mesh` 等原型，但其 `count=0`，没有提供正式世界实例坐标。本阶段只生成 `cliff_big_01` Mesh/材质资产，不捏造地图摆放。
- Unity 显示方式属于 B 级载体适配：构建器在 Editor 中保存 Mesh、材质和紧凑实例数据，`CK3DecorationRenderer` 在 URP 每个相机开始渲染时提交 GPU Instancing，并用 256 世界单位分块、视锥和可调水平距离裁剪同时支持 Scene/Game。临时材质使用 URP/Lit，树木沿用原版 `tree.shader` 已确认的 Alpha Cutoff `0.4`。它不会被描述为 CK3 原版渲染器，后续将由原版 `tree.shader`/`pdxmesh.shader` 移植替换。
- 2026-07-17 首次全世界验收修正：`building_locators.txt` 的省份 `10430` 保存非单位 Quaternion `{0,22.685270,0,85.983658}` 且 Scale 有两轴为 `0`，另有一条 locator 也含零 Scale。这些原始哨兵/无效变换不能进入 Unity `Matrix4x4.TRS`；Editor 导入与 Runtime 读取现在都会校验 Quaternion 长度和正 Scale，避免巨大白色网格在特定距离遮住地图。
- A 级树木颜色证据：`game/gfx/FX/tree.shader` 先取 `DiffuseMap`，再以实例矩阵随机种子采样 `TintMap`，执行 Overlay，最后按 `NormalMap.b` 混合颜色；单独显示灰白 Diffuse 不是原版结果。`CK3TreeSurface.shader` 现承载这段已确认核心、Alpha Cutoff `0.4` 和双面树叶，其余 CK3 Map Lighting/积雪/省份效果仍未完整移植。
- A 级缩放可见性证据：`game/gfx/map/map_object_data/layers.txt` 中 `tree_high_layer`、`tree_low_layer`、`building_layer` 的 `fade_out` 都为 `9`；`clausewitz/gfx/FX/cw/pdxmesh.fxh` 的 `PdxMeshApplyDitheredOpacity` 与 `game/gfx/FX/tree.shader` 的 `DitheredAlpha` 证明原版不是在第 9 档瞬间硬切，而是通过实例透明度和屏幕空间抖动逐渐减少可见像素。Unity 载体以第 9 档为淡出终点，并用可调的短时平滑把离散镜头档位转换为连续 `_GlobalOpacity`；这段时间平滑仅是 Unity 输入适配层，不声称属于 CK3 原版。
- A 级城堡材质证据：`game/gfx/models/buildings/holdings/building_western_castle_01.asset` 的 `meshsettings` 指定 `texture_diffuse = building_western_atlas_diffuse.dds`、`texture_normal = building_western_atlas_normal.dds`、`texture_specular = building_western_atlas_properties.dds`，实际纹理位于 `holdings/atlas/western/`。装饰构建器必须读取相邻 `.asset` 的材质元数据并在 holdings 子目录中解析 DDS，不能假定纹理名一定嵌入 `.mesh` 或与 Mesh 同目录。

### 15.2 当前水体实施顺序

1. **已完成证据门禁**：`water.settings`、`pdxwater.shader`、Jomini Include、`00_defines.txt` 与 `jomini_water.cpp` 已确认恒定水位、世界 UV、地形深度、纹理、Pass 状态和完整世界四角平面。
2. **已完成基础验收**：`CK3WaterSurfaceBuilder` 在 Editor 中保存完整世界静态四角海面、原版 BC7 水色图与材质；`CK3WaterSurface.shader` 移植 `waterLowSpec` 的九点高度、水深和岸边淡出；Runtime 不创建 Mesh/Material/Texture。Unity 载体已同步地形的平面地图高度过渡并关闭透明水面的深度写入，任意战略视角下基础海面稳定可见。
3. 继续恢复独立湖泊 Mesh 的原版生成证据；基础海平面以下的湖海先由全世界水面与真实地形深度自然显示，不能把 `default.map` 分类自行轮廓追踪成最终湖泊 Mesh。
4. 移植近景水体：地形深度、水色/高光图、三层波浪法线、Flow、Fresnel、反射/折射和岸边淡出。
5. 接入 `foam_map`、泡沫纹理及推进波线，再与政治距离场的 `WATER_BORDERS_ZOOM_STEP=8` 联合验收。
6. 水面稳定后进入河流：先恢复原版河网 Mesh 数据，再分别接河床、河面、13 级宽度和入海淡出。

## 16. 阶段证据与构建历史

本节记录已经完成过的调查和构建结果，用于追溯依据，不再作为实时任务列表。实时状态只看第 15 节。

### 阶段 1：建立 CK3 原版数据基线

状态：已完成并通过扩展基线验收。

目标：

- 在 `Assets/CK3Map/Data/Source` 保存当前地图范围所需的 CK3 原始数据副本。
- 导入 CK3 完整世界范围数据，不裁剪欧洲、测试区域或低分辨率代理数据。
- 保持原版相对目录，避免不同系统使用重复或来源不明的文件。
- 建立唯一的 Editor 导入/同步入口。
- 生成来源清单，记录每个文件的 CK3 路径、Unity 路径、大小和修改时间。
- 当前导入范围包括 `map_data`、Terrain、Water、Rivers、Borders 和法理头衔数据。

本阶段只复制和核对原版完整世界数据，不转换未知格式，不生成推测性的 Mesh。

CK3 的部分边界笔刷为 85×86 或 86×86 的 BC1/BC3 DDS，不满足 Unity 内置 DDS Importer 要求的 4×4 块尺寸倍数。Unity 源数据副本对此类文件追加 `.ck3source` 后缀，仅阻止内置 Texture Importer 误报；文件字节和原始 `.dds` 来源路径保持不变。后续必须由专用 Editor 转换阶段按已确认格式解码，不能重新压缩原游戏文件冒充源数据。

CK3 的 `game/gfx/FX`、`jomini/gfx/FX` 与 `clausewitz/gfx/FX` 中还包含 82 个 Paradox `.shader` 文件。这些文件使用 Clausewitz/Jomini Shader 语言，并不是 Unity ShaderLab；若保持 `.shader` 后缀，Unity 会错误调用 Shader Importer 并产生大量解析错误。源数据副本统一保存为 `.shader.ck3source`，原始字节不变，`source_manifest.csv` 仍同时记录原始 CK3 路径和 Unity 副本路径。该改名只隔离 Unity Importer，不改变任何原版 Shader 证据或 Include 关系。

首轮结果：

- 已复制 589 个 CK3 原版文件，源文件总大小 `1,395,368,544` 字节，逐文件总数和总字节数与游戏本体完全一致。
- 来源目录为 `Assets/CK3Map/Data/Source`。
- 来源清单为 `Assets/CK3Map/Data/Source/source_manifest.csv`。
- 唯一同步入口为 `CK3 Map/原版数据导入器`。
- 37 个 CK3 边界 DDS 以原始字节形式保存为 `.dds.ck3source`，避免 Unity 内置 DDS Importer 对非标准块尺寸误报。
- 82 个 Clausewitz/Jomini/CK3 原版 Shader 以原始字节形式保存为 `.shader.ck3source`，避免 Unity 把 Paradox Shader 语言误当 ShaderLab 编译。
- 本阶段没有裁剪世界范围、没有降低源图分辨率、没有生成任何 Terrain/边界/河流临时 Mesh。

2026-07-15 全根目录依赖复查发现首轮 589 文件虽然覆盖地图数据和主要美术资产，但遗漏了 `game/gfx/FX`、`jomini/gfx/FX`、`clausewitz/gfx/FX` 及地图编辑器/历史/本地化证据。唯一导入器已经扩展为 22 个不重叠来源目录，下一次同步的预期基线为 `3,021` 个文件、`1,710,458,204` 字节。新增范围包括：

- 完整 `game/gfx/map` 与三棵 FX 树。
- Clausewitz Terrain/Editor Terrain 资源和 Jomini Map Editor GUI/工具资源。
- 图形/Jomini Defines、字体、英文 Localization。
- landed titles、province terrain、culture、religion。
- titles、provinces、province mapping、characters 历史数据，用于后续区分法理层级与指定开局日期的实际统治状态。

用户已于 2026-07-15 执行“导入或同步 CK3 原版数据”并确认 `3,021` 文件。随后核对 `source_manifest.csv` 得到 `3,021` 行、`1,710,458,204` 字节；Terrain Shader、CK3 Province Overlay 覆盖、Jomini Distance Field、Clausewitz Terrain/Heightmap FXH 和 `nodes.dat` 六个关键文件均与游戏本体 SHA-256 一致。阶段 1 正式完成。

### 阶段 2：地表材质与 Terrain Shader

状态：基础 Terrain 材质与完整世界控制图已经完成并显示；政治覆盖已并入同一 Terrain Pass。2026-07-17 已接入 CK3 Map Lighting 第一版，等待 Unity 画面验收；动态季节/省份效果仍未完成。

目标：

- 按 CK3 的 105 个材质定义建立可追溯的 Unity 纹理数组和材质登记。
- 移植四层材质索引、权重聚合、diffuse Alpha 高度混合、normal/properties 混合。
- 移植 colormap SoftLight、已确认的地图光照输入和平面地图过渡。
- 分离基础材质和 drought、flood、summer_grass、winter_effect 等动态覆盖。

当前 Editor 构建实现：

- 唯一静态资产构建入口为 `CK3 Map/地图构建器`。
- `CK3TerrainMaterialBuilder` 直接解析同步后的原版 `materials.settings`、`settings.terrain` 与 `detail_data.settings`，强制验证 105 个唯一材质 ID、前五个动态材质的固定顺序、四层材料上限和所有引用文件。
- 三组 105 层纹理数组直接复制原版 1024×1024 DXT5 的 11 级 Mipmap 块数据，不经过 Unity 图片解码、Normal Map Importer 或重新压缩；漫反射数组使用 sRGB，RRxG 法线与材质属性数组使用线性采样。
- 输出是保存在 `Assets/CK3Map/Data/Generated/Terrain` 的三个 `Texture2DArray` 资产和 `CK3原版地表材质库.asset`，不在 Runtime 或 Play 模式创建。
- 构建器代码已通过 Unity 2022.3.62f1c1 自带 Roslyn 编译器检查；源数据独立解析复核得到 105 个条目、105 个唯一 ID，前五项依次为 `drought`、`drought_cracks`、`flood`、`summer_grass`、`winter_effect`。
- 第二次构建会额外把原版 `detail_index.tga` 与 `detail_intensity.tga` 的完整 9216×4608 BGRA 像素直接保存为 Point/Clamp/无 Mipmap 的线性 `Texture2D` 资产，并把 `colormap.dds` 的 14 级 DXT5 Mipmap 原样保存为线性采样资产，以便 Shader 显式执行原版 `pow(color, 2.2)`。
- `CK3TerrainSurface.shader` 已移植原版四邻 texel 同 ID 权重聚合、四层 diffuse Alpha 高度混合、共用混合权重、RRxG 法线重建、材质属性合成、Y 翻转 colormap 和 Pegtop SoftLight；2026-07-17 起继续进入下述 CK3 Map Lighting 第一阶段，不使用 Unity Lit 替代原版光照链。

2026-07-17 CK3 Map Lighting 第一阶段：

- A 级证据来自 `game/gfx/FX/pdxterrain.shader`、`game/gfx/FX/jomini/map_lighting.fxh`、`game/gfx/FX/cw/lighting.fxh` 和 `game/gfx/FX/cw/lighting_util.fxh`。
- 已按原版顺序执行政治色 Pre-Lighting 混合、`GetMaterialProperties` 通道解释、Pdx Simple Lighting 的 GGX 直射光、`environment_terrain_sunny.dds` 晴天地形 IBL，以及政治色 Post-Lighting 混合。
- 地表材质通道继续严格使用原版 `DetailMaterial.a=PerceptualRoughness`、`.g=SampledSpec`、`.b=Metalness`，并保留 `RemapSpec=0.25×SampledSpec`、粗糙度平方、金属度对漫反射/高光颜色的转换。
- 原版晴天地形常量直接来自 `map_lighting.fxh`：太阳颜色 `(1,0.9,0.8)`、太阳强度 `8`、IBL 倍率 `0.25`、高光倍率 `1`。
- 原版 CPU 提交的太阳方向、Cubemap Y 旋转和全局环境强度尚未由现有证据闭合。Unity 第一阶段只把场景 Directional Light 的方向、阴影衰减作为明确标记的载体适配；Cubemap 旋转保持单位矩阵，不能描述为原版 CPU 实现。
- 云影 Sunny/Overcast 双场景、`shadow_tint.fxh` 和 `ApplyOvercastContrast` 尚未进入本阶段，避免在第一轮视觉验收前继续扩大修改。
- `CK3TerrainSurfaceBinding` 只负责把已经持久化在材质库中的 105 个二维平铺常量上传给 Shader，不创建、克隆或生成任何 Runtime 资产；这是 Unity 承载原版 `PdxTerrainConstants.PackedDetailTileFactors` 数组的绑定层。

### 阶段 3：Terrain Mesh 与高度系统

状态：GPU 高度页表、节点拓扑、Skirt 与固定原版 `ForceLodLevel` 完整世界场景已经完成并显示。当前为便于视觉开发而保持完整世界全部可见；原版自适应 `CullAndLod` 仍待最终阶段恢复。

目标：继续调查 `CalcTerrainVertex`、Packed Heightmap、节点拓扑、LOD 和 Skirt。只有确认原版生成规则后，才创建 Editor Terrain 烘焙器并保存 Mesh/Collider 资产。

2026-07-15 新增的原版二进制证据：

- `CPdxTerrain` 初始化函数依次创建基础顶点缓冲、主索引缓冲和 Skirt 缓冲。
- 基础节点固定为 `33×33 = 1089` 个顶点，每顶点 8 字节：两个 `R16_UNORM` 的 `WithinNodePos`，以及两个 `R16_SNORM` 的 `LodDirection`。
- `WithinNodePos` 按 `round(coordinate × 65535 / 32)` 生成。
- 完整十级树的有效叶节点为 `576×288 = 165888`，与 `18432×9216` 原始高度图按每节点 `32×32` 单元严格对应；因此 `NormQuadtreeToWorld=16384`，叶节点覆盖 `16×16` 世界单位，每个单元对应原始高度图的 `0.5` 世界单位。
- `LodDirection.x` 在偶数列为 0、奇数列为 32767；`LodDirection.y` 在偶数行为 0，在奇数行按半分辨率行列棋盘奇偶取 32767 或 -32768。
- 主索引固定为 `32×32×6 = 6144` 个 16-bit 索引，即 2048 个三角形；每个单元按 `(row + column)` 奇偶交替选择对角线。
- 原版缓冲创建函数地址范围分别为：基础顶点 `0x143057360–0x1430575B0`、主索引 `0x143058210–0x143058AA8`、Skirt `0x1430575C0–0x14305820E`。这些函数由同一 Terrain 初始化链直接调用，不是通用网格代码的推断。
- Unity 的 `CK3TerrainNodeMeshBuilder` 在 Editor 中把上述拓扑保存为 `Assets/CK3Map/Data/Generated/Terrain/CK3原版地形节点网格.asset`。UV0 承载原版 `WithinNodePos`，UV1 承载原版 `LodDirection`；Unity 必需的 Position 通道使用同一个节点内平面坐标，后续仍由 `CalcTerrainVertex` 等价移植完成世界坐标和高度位移。

原版 `nodes.dat` 生成函数 `0x143065D10–0x1430661AB` 进一步确认：

- 每条 `SNode` 固定 32 字节，偏移 `0/4/8` 分别写入 X、Y 与节点 Scale。
- 子节点索引严格为 `4×父索引+1` 到 `4×父索引+4`，文件按完整广度层级连续排列。
- 偏移 `12/14` 是从原始高度输入量化得到的最低/最高高度；父节点直接合并四个子节点的最小值和最大值。
- 偏移 `20/22` 分别保存两种几何误差计算结果，其中偏移 22 由原版 `CalculateAbsoluteGeometricError` 写入；偏移 `24` 保存向父层累计后的浮点几何误差。
- 偏移 `28` 的字节由节点是否落在有效高度图范围内及子节点状态合并得到；原版遍历用它跳过无效节点。
- `Terrain.Quadtree.ForceLodLevel` 是 CK3 已存在的原版调试路径。当前 Editor 完整世界构建器先用该路径生成固定层级预览，默认第 7 层；它不是自制 LOD 规则。后续仍要移植原版自适应 `CullAndLod`。

Skirt 初始化时预分配 264 个顶点的容量，但逐指令复核确认实际写入 256 个顶点：上边 33 点、右边去除角点后 31 点、下边反向 33 点、左边去除角点后反向 31 点，每个外围点连续复制为一对。索引严格为 `0..255, 0, 1` 的闭合 triangle strip。`FixPositionForSkirt` 使用 `SkirtSize * ((VertexID + 1) % 2)` 偏移每对中的偶数顶点。Unity Mesh API 没有 triangle strip 拓扑，因此 Editor 构建器把同一索引条带无损展开为保持交替绕序的 triangle list；顶点、外围顺序和最终几何拓扑不变。

### 2026-07-16 剩余工作快照（已由第 15 节取代）

以下顺序按依赖关系执行；“代码完成”不等于“Unity 画面验收完成”。

1. 政治数据完整性：补齐 `h_` 等原版特殊 landed-title 层级，保证法理与指定日期实际领地覆盖完整世界；继续核对动态 title color/name effect，不能只使用静态默认色代替已经发生的历史效果。
2. 政治覆盖渲染：完成 CK3 游戏覆盖版五点距离场采样、同色深边公式、Pre-Lighting 混合、地图光照和 Post-Lighting 混合；近景到平面地图使用 `map_modes.txt` 的五组原版 GradientBorders 参数。
3. 地形光照：保留 CK3 地形高度法线和材质通道，接入 Unity URP 主光、阴影与环境光；Bloom、颜色调整、曝光和抗锯齿使用 Unity Volume/URP 组件，不在地图 Shader 内重复实现后处理。
4. 几何边界：恢复 CK3 连续边界拓扑生成器，Editor 持久化省份、公国、王国/Realm 与选中边界 Mesh；移植原版笔刷 DDS、UV、接头、分叉、闭环、镜头缩放、深度偏移、模板和 Alpha Blend。
5. 河流：恢复 `rivers.png` 图结构、13 个宽度等级和原版河床/河面 Mesh 生成；陆地河流可见，海洋无错误河流，并按原版处理入海淡出。
6. 海洋与湖泊：恒定海平面、地形深度、水色、波浪法线、Flow、Fresnel、反射/折射、海岸淡出、泡沫和推进波线；湖泊仅在原版 Mesh 规则确认后生成。
7. 地图名称：闭合 `realms=9` 的 Realm 节点/主头衔构建与逐字曲线弧长公式，使用 `Paradox_King_Script.otf` 的 SDF Atlas，在 Editor 生成可见、可选择、持久化的弯曲标签 Mesh；法理地图使用对应 dejure 名称源，不能拿水平 TextMesh 代替。
8. 交互与地图模式：完善 CK3 镜头高度驱动的近景地形—远景政治地图过渡、地图模式切换、点击/悬停/选中高亮；静态资产继续只在 Editor 构建。
9. 全世界联调：Scene/Game 一致、完整世界无缺块、边界连续、河流不串海、海岸正确、标签 LOD 正确；最终只保留 `CK3 Map/地图构建器` 一个构建入口。
10. 文档与证据：每次只登记已经由三套源码树、原版数据或 `ck3.exe` 静态分析确认的规则；不确定项明确保留为阻塞，不用经验实现冒充原版。

### 阶段 4：省份与行政区划数据

状态：已完成并进入政治地图与名称构建使用。

目标：

- 导入 `provinces.png`、`definition.csv`、`default.map` 和 `adjacencies.csv`。
- 解析 `landed_titles` 的 `e_/k_/d_/c_/b_` 嵌套和 `province` 关联。
- 明确区分法理层级、地图省份分类和动态实际统治状态。

2026-07-15 对当前完整本体输入进一步核验并实现：

- `provinces.png` 为 `9216×4608`、8-bit RGB、无交错 PNG；Editor 构建器按 PNG 原始行方向解码后翻转到 Unity 底行对应世界 Y=0，并保存为 Linear、Point、Clamp、无 Mipmap 的完整 RGB24 资产。
- `definition.csv` 当前有连续的 `0–13269` 共 `13270` 条有效省份定义；构建时强制检查编号连续、编号唯一，并逐像素验证 `provinces.png` 中的每种实际使用颜色都能唯一映射到定义表。原文件允许未使用颜色重复：编号 `0` 与不可通行编号 `12946` 都登记为 `(0,0,0)`，但完整 `provinces.png` 中黑色像素数量为 `0`，因此二者都只是当前地图未使用的定义。构建器保留两条原始记录，只有地图实际使用歧义颜色时才报错。
- `default.map` 的 `sea_zones`、`river_provinces`、`lakes`、`impassable_mountains`、`impassable_seas` 以可叠加位标记原样保存；原文件中少量高于有效 definition 最大编号的分类引用也保留，不伪造对应省份定义。
- `adjacencies.csv` 当前有 `353` 条哨兵行之前的特殊连接记录，保存 From、To、Type、Through、起止像素和原版 Comment。
- `landed_titles` 当前包含 `17456` 个 `e_/k_/d_/c_/b_` 声明、`11297` 个实际关联省份。解析器只把根级或头衔块直接子级的头衔声明当作法理嵌套，不会把触发器/效果块中的引用误认成行政层级。
- 当前原版 landed-title 还包含 5 个真实 `h_` 霸权头衔：`h_roman_empire`、`h_eastern_roman_empire`、`h_dar_al_islam`、`h_india` 与包住整棵中国法理树的 `h_china`。旧解析器遇到 `h_china` 时会把其内部全部 `e/k/d/c/b` 作为未知值跳过，导致中国在法理和实际颜色表中同时空白。2026-07-16 已把 `h_` 作为帝国之上的原版 landed-title 节点纳入法理父链与 title history；它不写入 e/k/d/c/b 的逐省等级数组，但可作为实际 liege 链顶点和颜色来源。
- `game/history/titles/e_china.txt` 还通过日期 effect 对 `h_china` 执行 `set_title_color` 与 `set_title_name`；1066.9.15 有效颜色来自 960.2.4 宋朝事件的 `{100,10,10}`，而不是 landed_titles 默认 `{230,180,10}`。实际领地构建器已按日期保存并优先使用直接 `set_title_color`，同时保留有效 `set_title_name` 本地化键供后续原版 Realm 标签使用；当前全套 title history 中直接 `set_title_color` 只出现在该中国文件。
- 原文件自身有重复的顶层无地头衔 `c_nf_dam` 与 `c_nf_to`，各出现两次；它们属于 landless 贵族家族标题且不关联省份。构建器保留全部原始声明，不擅自合并；逐省法理索引只由明确的 `province = ID` 父链生成。
- 头衔 `color` 当前保存原始 `RGB255`、`HSV01` 或 `HSV360` 编码和值，不在未确认颜色转换细节前提前烘焙为替代颜色。
- 输出为 `Assets/CK3Map/Data/Generated/Political/CK3原版省份数据库.asset`、完整省份 RGB24 纹理和 `CK3原版法理头衔层级.asset`，均由现有唯一 `CK3 Map/地图构建器` 在非 Play 模式持久化创建。

### 阶段 5：政治色块和边缘渐变

状态：F1/F2 完整世界填色已显示；原版距离场 GPU Pass、CPU 默认参数、递减传播调度、`fill_in_impassable`、游戏覆盖版五点采样和 Pre/Post Lighting 参数链已接入。含水岸与无水岸两套距离场已实现，等待真实水体完成后的联合画面验收。

目标：移植 Province Color Indirection、Primary/Secondary Overlay、CK3 覆盖版五点距离场采样、Pre/Post Lighting 和原版同色边缘渐变。

距离场和动态颜色纹理的生成方式确认前，不制作邻点 ID 近似方案。

### 阶段 6：几何边界

状态：边界拓扑生成证据未通过。

目标：

- 继续调查原版连续边界拓扑、接头、分叉、闭环、UV 和 Mesh 布局。
- 证据充分后在 Editor 中生成并保存边界 Mesh。
- 移植原版 DDS 笔刷、缩放层级、Height Offset、深度偏移、模板和 Alpha Blend。

### 阶段 7：河流

状态：河网拓扑、宽度、平滑、自适应曲线与第一版持久化河面 Mesh 已接入；等待 Unity 视觉验收。

2026-07-17 首次河面验收发现山区河段断续。审计确认河网 Mesh 本身连续，错误来自 Unity 河面载体在顶点 Shader 中对周围九个高度样本取平均，而 Terrain Shader 只读取当前位置的原版高度页。平滑后的河面在陡坡处低于真实地表并被深度测试遮挡。该无原版依据的九点平均已经删除，河面现在与 Terrain 使用同一位置、同一页表公式采样高度；只保留 `_RiverHeightOffset` 处理 Unity 共面深度精度。新增 `CK3RiverSurfaceBinding` 在场景中以中文 Inspector 暴露离地高度、颜色、透明度、纹理缩放、流速和入海淡出率。此修复属于 Unity 载体纠错，不声称恢复了原版 CPU 写入河流顶点 Y 的全部内部逻辑。

目标：

- 已新增统一构建器阶段 10：把完整 `rivers.png` 按原版批次和固定四邻域规则保存为 `Assets/CK3Map/Data/Generated/Rivers/CK3原版位图河网.asset`。
- 已新增统一构建器阶段 11：使用 `0x143BC3F10` 与 `0x143BC4370` 已确认的平滑和曲线规则生成 `Assets/CK3Map/Data/Generated/Rivers/CK3原版河面网格.asset`，创建可调材质并绑定为 Scene 中的 `CK3原版河流`。
- 继续定点闭合 `GenerateRiverData` 调用的平滑、细分与分支端点参数，然后由 Editor 生成持久化河床/河面 Mesh。
- 保留 13 个宽度等级、控制节点、Transparency、Width 和 DistanceToMain 数据含义。
- 证据充分后分别移植河床与河面，并处理与海洋连接的淡出。

### 阶段 8：海洋与湖泊

状态：基础海面已通过 Unity 联合验收。原版 `WATERLEVEL=3`、完整世界四角海面网格及 GPU 行为已定位；Unity Editor 构建器、BC7 解码、低规格水面 Shader 和场景绑定已经完成。2026-07-17 已针对平面地图深度遮挡补入 `_FlatMapLerp` 联动、关闭透明水面深度写入，并取消构建后自动选中巨大海面对象。高级水体与高位独立湖泊仍待实现。

目标：先持久化原版全世界四角海面并移植 `waterLowSpec`，再移植空间水色、三层波浪法线、Flow、Fresnel、折射、反射、海岸淡出、泡沫和推进波线。高位独立湖泊 Mesh 生成仍受原版证据门禁约束。

### 阶段 9：地图标签

状态：F1 法理王国与 F2 实际领地的原版字体曲线 Mesh 已在 Editor 持久化构建并可见；模式切换和地形贴合已接入。最终缩放 LOD、密度、尺寸与动态 Realm 更新仍待完成。

### 阶段 10：整体联调

状态：等待前置阶段。

目标：在完整 CK3 世界范围内统一地形、政治覆盖、边界、河流、海洋、标签、全世界分块加载和摄像机高度切换，最终由用户在 Unity Scene/Game 中确认视觉效果。局部测试通过不等于项目完成。

已确认的地图相机基线来自 `game/common/defines/graphic/00_graphics.txt` 的 `NCamera`：`FOV=60`、`ZNEAR=10`、`ZFAR=100000`、`START_LOOK_AT={5000,0,2300}`、`START_ZOOM_STEP=33`；第 33 档在原版数组中对应缩放距离 `5464` 与倾角 `85`。此前 README 和场景构建器误记为 `55`，现已按原始数组纠正。`ZOOM_STEPS` 与 `ZOOM_STEPS_TILT` 的全部 35 档、`SCROLL_SPEED=0.045`、`EDGE_SCROLLING_PIXELS=5`、`PANNING_WIDTH=9090` 和 `PANNING_HEIGHT=4696` 已由 `CK3StrategicMapCamera` 承载；Unity 只负责输入和 Transform 更新。

### 阶段 5 补充：已确认的政治填色与地图模式链路

原版证据：

- `game/map_data/provinces.png` 与 `definition.csv` 提供完整世界省份颜色到省份编号的映射。
- `game/gfx/FX/jomini/jomini_colormap.fxh` 的 `SampleProvinceId` 明确把间接寻址纹理的 RG 解码为 `R + G * 256`；`ColorSample` 再用同一 RG 作为 `256×256` 的 `ProvinceColorTexture` 坐标。
- `game/gfx/FX/jomini/jomini_province_overlays.fxh` 明确要求 `ProvinceColorIndirectionTexture` 使用 Point、U Wrap、V Border，颜色表使用 Point/Clamp，并通过 `BilinearColorSample` 手工插值相邻四个省份颜色。
- CK3 的 `game/gfx/FX/jomini/jomini_province_overlays.fxh` 覆盖版启用 `BORDER_DISTANCE_FIELD_SAMPLES_MEDIUM`，使用中心加四个对角点的五点距离场采样；Jomini 通用文件启用的 High 九点分支不是 CK3 当前配置。`CalcPrimaryProvinceOverlay` 使用同色的 `gradient_color_mult` 与 `edge_color_mult` 形成边缘深浅渐变。
- `game/gfx/map/map_modes/map_modes.txt` 明确给出 `dejure_kingdoms`、`realms`、`simple_realms` 的 color mode、`fill_in_impassable` 和缩放档位 2/9/15/20/21 的 Gradient Parameters。
- `game/history/titles` 的 `holder` 与 `liege` 日期块提供指定开局日期的实际头衔统治关系；当前预览日期固定为 `1066.9.15`。

当前 Unity Editor 构建实现：

- 阶段 7 把完整 `9216×4608` 省份图持久化转换为 RG16 省份编号间接寻址纹理，不在 Runtime 生成。
- 为每个省份编号生成 `256×256 RGBA32` 法理王国颜色表与 `1066.9.15` 实际领地颜色表。
- 法理颜色直接来自 `landed_titles` 的王国头衔颜色；`hsv` 转换逐式移植 `clausewitz/gfx/FX/cw/utility.fxh::HSVtoRGB`。
- 实际领地颜色由县头衔开始，按指定日期有效的 `liege` 链追到顶层头衔，再使用该头衔颜色。历史事件中的嵌套 effect 不被误当成直接 `holder/liege` 赋值。
- `CK3TerrainSurface.shader` 已接入原版间接寻址、颜色表查找、`BilinearColorSample`、五点距离场读取和 `CalcPrimaryProvinceOverlay`。原版 Map Lighting 尚未移植，当前 Pre/Post Lighting 的执行顺序已经保留，但中间照明阶段仍为恒等阶段。
- 场景根对象上的 `CK3MapModeController` 在非 Play 模式也会把持久资产绑定到共享材质；Play 模式用 F1/F2 在法理王国与实际领地之间切换。

仍需继续确认：`realms` color mode 对同一角色同时持有多个互不隶属顶级头衔时，如何选取该角色的最终 primary title 颜色。当前历史预览严格按显式 title liege 链着色，不用猜测的 primary-title 自动选择规则合并这些顶级头衔；后续找到引擎证据后再补齐这一小类状态。

2026-07-16 首次完整世界政治填色验收后的结论：

- 当前颜色表中的法理头衔颜色仍直接来自原版 `landed_titles`，HSV 也沿用已确认的原版转换式；目前没有证据表明基础色号读取错误。画面与原版的主要差异来自渲染链尚未完成：现有 Unity Shader 把 Pre/Post Lighting 参数合并成一次光照前 `lerp`，而原版明确先执行政治色的 Pre-Lighting 混合，经过 CK3 Map Lighting 后再执行 Post-Lighting 混合。因此当前只能验收省份寻址和大色块，不能验收最终颜色观感。
- 首次验收时没有描边和同色暗边不是构建失败。当时尚未接入原版 `CalcPrimaryProvinceOverlay` 所依赖的 `ProvinceColorBorderDistance`。此后已按 CK3 游戏覆盖版的 Medium 分支接入五点距离场采样，并使用 `edge_width`、`edge_sharpness`、`edge_alpha`、`edge_color_mult` 形成连续同色暗边。
- 法理王国、法理帝国和国家级地图模式在原版 `map_modes.txt` 中明确使用 `fill_in_impassable = yes`。`ck3.exe` 的 `0x140db53b0–0x140db5719` 已恢复对应的 `uint` 颜色表特化：只处理 `IsImpassable` 且像素数小于 `LARGE_IMPASSABLE_PROVINCE_PIXELS` 的省份；当前版本该虚函数默认值由 `0x143322eb0` 明确返回 `100000`。
- `fill_in_impassable` 不做最近颜色扩散，也不通过其他不可通行省份继续传播。它按省份连接表依次检查邻居，只接受 `!IsImpassable && IsLand` 的邻居，按完全相同的 32 位颜色值分组，每条连接计一次；第一次出现的候选在票数并列时保留。只有最高票严格大于全部合格连接数的一半时才覆盖当前不可通行省份颜色，否则保持原值。
- 普通连接表的来源与顺序也已闭合：`0x143320e70` 从完整省份图生成每省 6 字节像素跨度，`0x142077110` 调度普通邻接构建，工作函数 `0x1420772b0–0x1420774cb` 根据跨度外围的上、下、左、右像素建立四邻接标记，再按省份编号从 `1` 开始递增写入，故普通邻居严格按 ID 升序且去重。`0x1420753e0–0x142075bff` 随后读取 `adjacencies.csv`，只把尚不存在的特殊连接按文件顺序追加到两端省份。Unity Editor 构建器现已按同一数据、四邻接、排序、追加顺序、阈值、候选分组和严格多数条件生成法理与实际颜色表。
- `game/history/province_mapping` 不是政治地图不可通行区域归色表；其中阿拉伯文件里关于不可通行沙漠/山脉的对应关系还是注释，不能把它当作原版 `fill_in_impassable` 的实现证据。
- `CK3StrategicMapCamera` 已使用原版 `SCROLL_SPEED = 0.045`，但当前 `scrollSpeed * zoomDistance * UnityDeltaTime` 是尚未由原版 CPU 代码确认的 Unity 适配公式。首次 Game 验收确认它明显过慢，因此这属于相机适配 bug；不能通过篡改 `SCROLL_SPEED` 或随意加入经验倍率宣称已复刻，下一步必须恢复原版 `CJominiCamera/CGameCamera` 的输入到世界位移换算。

上述 2026-07-16 顺序已完成其中的 `fill_in_impassable` 与曲线名称基础实现；当前顺序由第 15 节取代。Unsigned Distance Field、Primary Province Overlay 的五点采样、同色暗边和 Pre/Post Lighting 参数链已经接入，下一次联合验收在真实水体完成后进行。

2026-07-16 距离场移植进度：

- 新增 `Assets/CK3Map/Shaders/CK3ProvinceDistanceField.shader`，逐阶段承载原版 `jomini_unsigned_distancefield.shader` 的 `WildCardMask`、`Init`、`InitWithWildcards`、`Fill` 和 `Finalize` 五个 Effect。保留原版固定四倍降采样、8×8 初始化搜索、RGBA 颜色平方差阈值 `0.0001`、八方向传播和最终归一化距离计算。
- 原版地图文件 `game/map_data/heightmap.heightmap` 明确给出 `should_wrap_x=no`；Unity 距离场 Shader 因此按原版未启用 Wrap 宏的分支对 X/Y 采样坐标执行 Clamp，没有把地图两端错误连接。
- 当前 `ck3.exe` 的 GradientBorders 默认状态块已定点恢复：`MaxDistance=127`、`IterationsPerFrame=8`、`UseIncreasingSampleWidth=true`、`WildCardSampleWidth=10`、`WildCardSamples=4`。原版更新函数对四倍降采样后的距离使用 `ceil(log2(MaxDistance/4))=5` 次传播，实际 SampleOffset 依次为 `16、8、4、2、1`；这解释了它不是逐像素扩散，也不会产生此前自制边界 Mesh 的断口和点状接头。
- `CK3ProvinceDistanceFieldBuilder` 现在在非 Play 模式对法理王国和 1066.9.15 实际领地分别执行 `Init → Fill×5 → Finalize`，把两张 `2304×1152` 距离场保存到 `Assets/CK3Map/Data/Generated/Political`。场景不在 Runtime 动态创建这些资产。
- `CK3MapModeController` 在 F1/F2 切换时同步切换颜色表和距离场，并逐值使用 `map_modes.txt` 中缩放档位 `2/9/15/20/21` 的 Gradient Alpha、Width、Edge Width、Edge Sharpness、同色倍率与 Pre/Post Lighting Blend。
- Wildcard Effect 和原版默认参数已经移植，但政治主色距离场何时向 Jomini 提交具体 WildcardColors 的调用点尚未闭合；当前法理/实际主色构建没有擅自指定 WildcardColors。不可通行区域归色已按上述已确认 CPU 实现接入 Editor 构建，等待画面验收。

2026-07-17 对安纳托利亚等国家内部出现洞状深边进行数据、算法和渲染联合审计：洞状区域对应 `default.map` 的 `lakes`，包括省份 `1481 WEST TURKISH LAKES` 与 `1482 EAST TURKISH LAKES`，不是漏归属的普通陆地。`fill_in_impassable` 只处理 `IsImpassable` 陆地，不能把湖泊强行继承给国家。错误来自此前距离场只执行 `Init`，透明湖泊颜色被当成另一政治颜色而生成深边。原版 `NBorder.ENABLE_GRADIENT_BORDERS_SHORELINE=yes`、`NMapModes.WATER_BORDERS_ZOOM_STEP=8`，同时 UDF 明确提供 `WildCardMask → InitWithWildcards`。Unity 端现按 `NMapColors.OCEAN_MAP_COLOR={0,0,0.1}` 与 `WATER_MAP_COLOR={0.67,0.6,1}` 给仅供距离场使用的海洋/内陆水域哨兵颜色，使用已确认的 `WildCardSamples=4`、`WildCardSampleWidth=10` 构建无水岸距离场；档位 8 前保留水岸边缘，达到档位 8 后切换无水岸距离场。政治颜色表本身不修改，水域仍不属于国家，无主普通陆地也不会被误当水域屏蔽。

2026-07-16 Inspector 调节约定：

- `CK3MapModeController` 暴露政治填色开关、整体强度、距离场五点采样偏移、五个缩放分界档位，以及 Near/Mid/Far/PreFlat/Flat 五套完整 GradientBorders 参数。每套均可直接调整渐变内外透明度、渐变宽度、渐变颜色倍率、深色边缘宽度/柔和度/透明度/颜色倍率和 Pre/Post Lighting Blend；默认值逐项来自原版 `map_modes.txt`。
- `CK3StrategicMapCamera` 只承载 Unity Camera 没有的 CK3 战略地图控制：观察中心、初始档位、缩放距离与倾角修正、WASD/边缘移动速度倍率、按键、滚轮步数和地图移动范围。`SCROLL_SPEED=0.045` 保持原版值，另设明确标注的 Unity 预览移动倍率，不把经验倍率冒充原版参数。
- `CK3TerrainSurfaceBinding` 暴露 CK3 Shader 特有的地形高度缩放、整体高度偏移、裙边深度、四层高度混合范围和宏观颜色影响强度，并可在 Play 模式持续上传以便实时观察。
- 不重复包装 Unity/URP 已有组件。FOV、裁剪面和投影继续直接使用 `Camera` Inspector；Bloom、曝光、饱和度、对比度、Tonemapping、Vignette 等继续直接使用 `Global Volume` 的 Profile；抗锯齿、后处理开关、抖动等继续使用 URP Additional Camera Data。
- 当前 Terrain 使用 URP `UniversalForward` 不透明 Pass，政治覆盖在同一 Fragment 中完成并写入相机颜色缓冲，因此会正常经过之后的 URP Volume 后处理。Bloom 是否明显仍由最终 HDR 亮度与 Volume Threshold 决定，Shader 不绕过后处理。

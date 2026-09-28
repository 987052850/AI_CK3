# CK3 地图原版二进制证据索引

本文件只登记 `binaries/ck3.exe` 中能够直接提取的类名、调试项、资源路径、编译单元路径和错误信息。它们可以证明原版 CPU 系统、数据流和阶段存在，但不能替代未公开的 C++ 函数正文。

## Terrain

- 编译单元：`clausewitz/pdx_terrain/pdx_terrain.cpp`
- 编译单元：`clausewitz/pdx_terrain/pdx_terrain_quadtree.cpp`
- 编译单元：`clausewitz/pdx_utils/pdx_packedheightmap.cpp`
- 编译单元：`clausewitz/pdx_terrain/pdx_materials_settings.cpp`
- 类/结构：`CPdxTerrain`、`CPdxTerrainQuadTree`、`CPdxTerrainQuadTree::SNode`、`CPdxTerrain::SCullAndLodData`、`CPdxTerrain::STerrainVertex`
- 数据：`map_data/nodes.dat`、`packed-heightmap`、`indirect-heightmap`
- 阶段：`InitHeightmapCache`、`CalculateAbsoluteGeometricError`、`CalculateNextLevelGeometricError`、`CullAndLod`
- Map Editor 明确提示：Save As 时勾选 nodes 可重新生成 `game/map_data/nodes.dat`
- Packed Heightmap 原版阶段包括 `CalcTileProperties`，并直接输出 tile size、compression levels、indirection map size、packed heightmap size、empty tile count 和各压缩层 tile count。
- Map Editor 的 `mapeditor_detail_data.cpp` 直接处理 `detail_index.tga`、`detail_intensity.tga`，并提供 `ProcessMasks`、`ExtraBorder`、`UpdateTextureBorder`。
- 调试/设置：`Terrain.Quadtree.UseLodLerp`、`UseCache`、`ForceLodLevel`、`LodSwitchFactor`、`ExtraTessellation`、`ScreenSpaceErrorThreshold`、`LeafLevelFadeFactor`、`ScreenHeight`、`DistanceLodFactor`

### Terrain 地表常量 CPU 打包

对当前 `ck3.exe` 的 `pdx_terrain.cpp` 代码段进行定点反汇编后确认：

- 原版先计算 `Terrain.XExtent / detail_tile_factor`，再用 `1.0 / 结果` 得到 X 平铺分量，并通过符号位异或得到负的 Y 分量。因此默认与逐材质数组均为 `(tile_factor / XExtent, -tile_factor / XExtent)`。
- 初始化阶段先用默认 `detail_tile_factor` 填满 `PackedDetailTileFactors`，随后对 `materials.settings` 中具有独立 `tile_factor` 的条目执行相同公式覆盖。
- `WorldSpaceToTerrain0To1` 直接写为 `(1 / XExtent, 1 / ZExtent)`。
- `WorldSpaceToDetail` 写为 `((DetailTextureSize - 1) / DetailTextureSize) * WorldSpaceToTerrain0To1`；CK3 的 9216×4608 控制图与 9215×4607 世界端点使其化简为 `(1 / 9216, 1 / 4608)`。
- 同一常量更新函数还上传 `DetailTexelSize`、`DetailTextureSize`、`WorldExtents`、`DetailTileOffset` 和 `DetailBlendRange`，与 `clausewitz/gfx/FX/cw/pdxterrain.fxh` 的 `PdxTerrainConstants` 字段顺序相符。

## 政治边缘距离场

- 编译单元：`jomini/modules/province_map_interface/source/jomini_gradient_borders.cpp`
- 类：`NJominiMap::CUnsignedDistanceField`
- 渲染资源：`gfx/FX/jomini/jomini_unsigned_distancefield.shader`
- Render Texture/资源名：`wildcard_mask`、`delta_vector_ping_pong0`、`delta_vector_ping_pong1`、`distance_field`
- 阶段/Effect：`WildCardMask`、`InitWithWildcards`、`Finalize`
- 调试/设置：`GradientBorders.FullRebuild`、`Enable`、`WildCardEnabled`、`WildCardSampleWidth`、`AlwaysRestart`、`IterationsPerFrame`、`WildCardSamples`、`MaxDistance`、`AlwaysDirty`、`UseIncreasingSampleWidth`
- 调试信息表明系统维护修改矩形并可逐帧迭代，不是一次性静态贴图。
- 当前 `ck3.exe` 中上述编译单元的直接代码引用集中在 RVA `0x02F7F889–0x02F807F1`；距离场调试量的注册代码可分别追到 `GradientBorders.UseIncreasingSampleWidth=0x0060443F`、`MaxDistance=0x006047EF`、`WildCardSamples=0x006049FF`、`WildCardSampleWidth=0x00604C0B`。
- 上述注册函数绑定到同一默认状态块 RVA `0x04F7F738`：`MaxDistance=127`、`IterationsPerFrame=8`、`UseIncreasingSampleWidth=true`、`WildCardSampleWidth=10.0`、`WildCardSamples=4`。更新函数 RVA `0x02F7E4D0` 在 Increasing 分支计算 `ceil(log2(MaxDistance*0.25))=5` 次迭代；Fill 提交函数 RVA `0x02F7EE20` 按剩余迭代指数生成 `SampleOffset=(-2^n,0,2^n)`，因此完整重建的步宽依次为 `16、8、4、2、1`。
- `game/map_data/heightmap.heightmap` 明确写有 `should_wrap_x=no`；因此当前 CK3 地图构建距离场时不得把世界左右端环绕连接。原版 Shader 在未定义 `JOMINI_UNSIGNED_DISTANCE_FIELD_WRAP_X/Y` 时对两轴执行 Clamp。

## 几何边界

- 编译单元：`jomini_border_interface.cpp`
- 编译单元：`jomini_border_manager.cpp`
- 编译单元：`jomini_border_generation.cpp`
- 编译单元：`border_segment_extractor.cpp`
- 类：`NJominiBorders::CBorderManager`
- 阶段：`GenerateBorders`、`ExtractSegments`、`AddStaticBorders`、`UpdateBorders`、`SyncBorders`
- 原版错误信息：`Border search failed to find next border pixel (corrupt province image?)`
- 调试/设置：`Border.Draw`、`DrawDebugPoints`、`ClusterDraw`、`LockCulling`、`Wireframe`、`Border.Parallel`

## 位图河流

- 编译单元：`jomini_river_graphics.cpp`
- 编译单元：`maprivers.cpp`
- 类/任务：`CJominiRiverGraphics`、`CRiverInitTask`、`SRiverDrawData`、`SRiverProvinceIntersection`
- 阶段：`GenerateRiverData`、`CalculateRiversByProvince`
- 设置：`Rivers.SmoothIterations`、`SmoothKernelSize`、`TessellationMinDistance`、`TessellationMaxAngle`、`SmoothFadeDistance`
- 校验：river bitmap 必须与 height map 同分辨率；系统区分 main/secondary river source、父河流和 river type。
- A 级解析入口：当前 `ck3.exe` 的位图河网解析函数位于 VA `0x1430445F0–0x1430450B0`。该函数直接引用分辨率校验、图像数据错误、`IsMainRiverSource`、`IsSecondaryRiverSource`、父河流和河流类型检查，不再只是由字符串推断阶段存在。
- A 级邻域规则：VA `0x143043C40` 与父河查找函数 `0x1430444F0` 均严格按左 `(x-1,y)`、右 `(x+1,y)`、上 `(x,y-1)`、下 `(x,y+1)` 顺序检查有界四邻域，不使用对角线，也不执行 X 环绕。
- A 级源点规则：解析器先逐像素扫描完整位图；调色板索引 `0` 作为主河源，`1/2` 进入次级源列表。主河全部建立后，次级源按原扫描顺序分批查找相邻的已归属父河点；一批查找完成后才建立该批河流，再开始下一批。无法找到父河的剩余源点在无进展时停止处理，当前 CK3 `rivers.png` 中有 3 个此类索引 `1` 标记。
- A 级中心线规则：VA `0x143043D00–0x1430444E4` 从源点开始，按上述固定邻域顺序选择第一个未访问且索引处于 `3..253` 的像素；`0/1/2` 不作为普通中心线，`254/255` 是水域/陆地背景，不进入河流。次级河流路径在自身源点前保存相邻父河点。
- A 级宽度规则：VA `0x14301DBC0` 对索引 `<3` 返回固定 `0.01`；索引 `>=3` 使用 `(WIDTH_MAX-WIDTH_MIN)*(index-3)/NUM_WIDTH_PIXEL_VALUES+WIDTH_MIN`。CK3 覆盖配置 `game/common/defines/jomini/rivers.txt` 给出 `NUM_WIDTH_PIXEL_VALUES=13`、`WIDTH_MIN=1`、`WIDTH_MAX=4`、`UV_SCALE=0.8`、`FADE_IN_DISTANCE=10`、`FADE_OUT_DISTANCE=5`。
- A 级路径转换：VA `0x14301E5A0–0x14301ED96` 把整数像素路径转换为 `float x/y/width` 路径；首尾保留像素位置，内部位置和宽度使用相邻输入的二点平均，支流起点还会结合父河方向调整。
- A 级最终顶点阶段：`GenerateRiverData` 位于 VA `0x14301EDA0–0x14301FAE3`；最终顶点构建函数 VA `0x14301DEF0–0x14301E1A0` 从曲线路径采样 Terrain 高度，生成与 `VS_INPUT_RIVER` 相符的 56 字节顶点记录。通用平滑/细分函数及分支端点的全部参数语义仍在继续闭合，正式河面 Mesh 不得绕过该剩余证据。
- A 级引擎默认值：`GenerateRiverData` 在 `0x14301F325–0x14301F348` 从 VA `0x144F3D298` 复制完整 32 字节曲线设置块。字段依次为：曲线类型 `0`、`SmoothIterations=2`、`SmoothKernelSize=1`、默认 `SmoothFadeDistance=2`、`TessellationMaxAngle=0.15`、`TessellationMinDistance=0.01`、`TessellationMaxDistance=1.0`、附加切线检测开关 `1`。随后该河流管线在 `0x14301F341–0x14301F348` 把附加切线检测开关改为 `0`，并把 `SmoothFadeDistance` 改为河流专用值 `5`；正式河流生成必须使用修改后的设置，而不是只使用注册默认值。
- A 级平滑算法：`0x143BC3F10–0x143BC436F` 对 `(x, y, width)` 三个 float 通道执行 `2` 轮、半径 `1` 的等权移动平均；越界样本由 `0x143BC57E0` 按首两点/末两点线性外推。范围外控制点原样复制，范围内结果在首尾 `5` 点内按到边界的归一化距离与原路径线性混合。
- A 级细分算法骨架：`0x143BC4370–0x143BC5400` 对每个相邻控制点段读取 `P[-1]..P[2]`，用 `0x143BC5980` 计算 Catmull-Rom 位置、用 `0x143BC5E00` 计算导数，并通过显式 48 字节栈记录对参数区间反复二分。接受条件直接读取上述 `0.15/0.01/1.0/false` 字段；正式 Mesh 仍需继续闭合 20 字节输出点记录到 56 字节河流顶点的字段映射。

## Spline River（与 CK3 位图河流分开登记）

- 编译单元：`jomini_spline_river_builder.cpp`、`jomini_spline_river_graphics.cpp`
- Map Editor：`spline_river_tool.cpp`、`spline_river_interaction_mode.cpp`
- 操作：连接河流、延伸河流、创建 tributary/distributary/main river、创建 anchor/strip。
- 资产数据库预期路径：`gfx/map/spline_network/spline_types`
- 当前游戏目录未发现 CK3 世界河网对应的 `game/gfx/map/spline_network` 资产，因此此通用系统不能替代 `rivers.png` 管线。

## 普通海洋水面

- A 级配置证据：`game/common/defines/00_defines.txt` 的 `NJominiMap` 明确给出 `WORLD_EXTENTS_X=9215`、`WORLD_EXTENTS_Y=50`、`WORLD_EXTENTS_Z=4607`、`WATERLEVEL=3`。
- A 级 Shader 证据：`game/gfx/FX/jomini/jomini_water_default.fxh` 的 `JominiWaterVertexShader` 将输入平面位置映射为 `(x, _WaterHeight, y)`，并使用 `(x / MapSize.x, 1 - z / MapSize.y)` 生成世界水色 UV。
- A 级 Shader 证据：`game/gfx/FX/pdxwater.shader` 的 `waterLowSpec` 使用 `GetHeightMultisample(WorldSpacePos.xz, 0.65)`，令 `Depth = waterY - terrainHeight`，再按 `1-saturate((FadeDepth-Depth)*FadeSharpness)` 得到岸边 Alpha，并直接采样 `WaterColorTexture.rgb`。
- A 级 Pass 证据：`jomini_water_default.fxh` 使用 SrcAlpha/InvSrcAlpha、RGB WriteMask 和 `DepthBias=-100`。
- B 级 Unity 载体结论：CK3 原生 Rasterizer `DepthBias=-100` 的整数单位与 ShaderLab Offset 数值换算尚未找到直接证据。`Offset 0,+100` 已由实际验收否定。Unity 2022.3 官方文档明确 `units` 只产生恒定偏移，`factor` 处理随镜头倾斜变化的最大 Z 斜率，并以 `Offset -1,-1` 作为避免共面深度冲突的示例；该值不得描述为 CK3 原版参数。2026-07-17 的 Scene/Game 验收确认仅靠该 Offset 无法跨越地形 `FLAT_MAP_HEIGHT=3.92`，且透明完整世界平面写深度会造成整面截断。当前 Unity 载体保留真实水深计算的 `WATERLEVEL=3`，只把渲染顶点随 `_FlatMapLerp` 过渡到 `_FlatMapHeight+0.02`，并关闭透明水面 ZWrite；`0.02` 明确属于 B 级 Unity 适配值。
- A 级二进制证据：当前 `ck3.exe` 的 `jomini_water.cpp` 构造代码位于 VA `0x142FAB990` 附近；水体源文件字符串引用位于 `0x142FAB5BD`，`gfx/map/water/water.settings` 字符串引用位于 `0x142FABA1B`。构造代码写入四个 `int2` 顶点，顺序为 `(xmin,ymin)`、`(xmin,ymax)`、`(xmax,ymin)`、`(xmax,ymax)`，顶点数固定为 4，以完整世界三角形条带提交。
- Unity 不支持 Triangle Strip MeshTopology 时，将同一四角平面展开为两个三角形属于索引承载适配；几何位置、范围和顶点顺序含义不变。
- 高于基础海面的独立湖泊 Mesh 生成函数仍未闭合，不得自行使用省份轮廓追踪替代。

## 地图名称

- Jomini 编译单元：`countryname.cpp`
- CK3 编译单元：`source/interface/mapgraphics.cpp`
- 数据/阶段：`Name._Vertices`、`UpdateMapNameData`、`UpdateMapNameMode`、`UpdateNamesIfNeeded`、`FillBaronyNames`
- 绘制：`CJominiMapGraphics::DrawMapNames`、`gfx/FX/mapname.shader`
- A 级 Shader 证据：`mapname.shader` 使用 `DepthEnable=no`；`jomini/countrynames.fxh` 直接把 CPU 输入 `Position.xyz` 作为世界位置，只对 Y 执行 `lerp(y, FlattenTo, FlattenAmount)`，没有额外名称高度抬升。
- B 级 Unity 载体结论：为消除 Editor CPU 烘焙高度与 Terrain GPU 页表高度的任何数值偏差，Unity 名称 Shader 直接复用 Terrain Shader 的高度纹理、五级页常量和同一 `CK3GetHeight` 函数，并在之后执行相同 Flat Map 过渡；这不改变原版名称 XZ/曲线布局，只保证 Unity 载体中的最终 Y 与可见地形严格同源。
- 名称模式符号表：`0x1442c06d0–0x1442c078f`，其中 `realms` 是 `UpdateMapNameData` 的模式 `9`。
- Realm 模式分派：`0x140b107b0` 选择 `0x140b1e0e0`（合并节点编号）与 `0x140b1e120`（本地化显示名）；通用收集器为 `0x140b1db80`。
- Realm 父节点解析：`0x140b00f00`，父节点编号读取回调为 `0x140967620`，Realm 选择器为 `5`。
- Realm 连通域分组：`0x140b20ed0`，沿实体 `+0x40` 邻接表洪泛，只合并 Realm 键相同的相邻实体；不相连领土分开生成名称候选。
- 主头衔脚本读取：`GetPrimaryTitle` 注册回调正文 `0x1425f3350`；优先读取对象 `+0x1b8` 中 `+0x1e0/+0x1ec` 有序列表首项，备用路径为对象 `+0x1c8` 中 `+0x68/+0x74` 列表首项，再经 landed-title 对象池解析。
- Jomini 区域布局主函数：`0x142fb2070–0x142fb3d8f`；控制点生成：`0x142fb04c0–0x142fb0f0e`；逐字曲线变换：`0x142fb0f10–0x142fb1bb0`；字体图集分组：`0x142fb1d10–0x142fb2061`。
- `0x142fb0f10–0x142fb1bb0` 进一步确认字形输入按每六个顶点、每顶点 `0x1c` 字节排列；曲线参数来自字体引擎水平排版后字形 X 中心相对整行跨度的归一化位置，不按弧长重新平均。三次曲线一阶导数生成切线/法线，六个顶点分别变换并采样地形高度。
- `0x142fb0f10` 的曲线不是单段 Bezier：五个控制点按参数 `0.5` 分成两段 Catmull-Rom，每段使用相邻四点；`0x142fb3300–0x142fb36e9` 则使用原版 `SCALE_CAP_WIDTH`、`SCALE_CAP_HEIGHT`、`MAX_STRETCH_FACTOR`、两个方形阈值和最终 `COUNTRY_NAMES_SCALE` 完成字体缩放与字符间距。原版 Defines 注册点位于 `0x142ffe990–0x142fff928`。
- `0x142fb21f6–0x142fb2271`：名称区域采样步长从 `COUNTRY_NAMES_STRIDE_SIZE` 开始，先在 `stride² > pixelCount` 时减半，再在 `pixelCount / stride² > 65536` 时翻倍。
- `0x142fb2288–0x142fb2d01`：逐像素累计 `x/y/xy/x²/y²`，分别构造 Y-on-X 与 X-on-Y 两条回归轴；不是 PCA 特征向量。
- `0x142fb2e03–0x142fb330a`：每条轴按 `-COUNTRY_NAMES_NUM_LINE_TESTS … +COUNTRY_NAMES_NUM_LINE_TESTS` 测试平行线，使用原版间距和整数栅格线，只保留 Area 内最长连续段。
- `0x143bbbdd0`：整数 Bresenham 线栅格器，输出连续 `int2` 像素数组。
- `0x142fafd70`：把栅格像素的 16-bit Province ID 映射为省份对象；区域统计仍只使用 Area 省份位集，截线有效条件为 `Area 省份 OR 大河省份 OR（Area 水域选项开启时的海区/湖泊）`，再依据目标像素位置选择连续有效段。直接调用链为 `0x142fb3e10 → 0x142fb2070 → 0x142fb0f10/0x142fb04c0 → 0x142fafd70`；`+0x1b` 为普通可通行陆地，`+0x1c` 为不可通行。
- `0x142fb3717–0x142fb3b7e`：20 字节记录为 `float3 Position + float2 TexCoord` 名称顶点；每字形严格六顶点。
- `0x142fb04c0`：按六顶点字形重新求区域法向截面，形成首截面、中间截面平均与末截面，再用单位方向、点积和二维叉积外推两端切向点，输出五个 Catmull-Rom 控制点。
- `0x142fb3717–0x142fb3b7e` 为每条区域截线构造六个 20 字节记录，`0x142fb04c0–0x142fb0f0e` 以六项分组、真实区域边界交点和首/尾/中间组结果构造五个控制点。该记录字段与两段 Catmull-Rom 路径已经闭合并用于当前 Unity 持久名称 Mesh；动态 Realm 更新仍未闭合。

## Shader 依赖闭包

七个地图核心入口的递归 Include 结果见 `CK3MapShaderDependencyIndex.txt`：共解析 50 个文件、148 条唯一 Include 边、0 个未解析依赖。入口为 Terrain、Water、Border、River Surface、River Bottom、Map Name 和 Unsigned Distance Field。

using System;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    public sealed class CK3MapBuildWindow : EditorWindow
    {
        private int forceLodLevel = 7;
        private Vector2 scrollPosition;
        [MenuItem("CK3 Map/地图构建器")]
        private static void OpenWindow()
        {
            CK3MapBuildWindow window = GetWindow<CK3MapBuildWindow>();
            window.titleContent = new GUIContent("CK3 地图构建器");
            window.minSize = new Vector2(620.0f, 330.0f);
            window.Show();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("CK3 完整世界地图构建器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "本窗口是地图静态资产的统一构建入口。所有结果保存在 Assets 中，不在 Play 模式临时创建。",
                MessageType.Info);

            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField("阶段 1　原版数据基线", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("状态", "原 3,021 个文件已验收；新增水位定义后应为 3,022 个文件");
            EditorGUILayout.HelpBox(
                "水体阶段新增 game/common/defines/00_defines.txt（原版 WATERLEVEL 与世界范围）。请在原版数据导入器中再同步一次。",
                MessageType.Warning);

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 2　原版地表材质", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", "105 套 DXT5 材质 + 完整世界索引/权重图 + colormap");
            EditorGUILayout.LabelField("输出", CK3TerrainMaterialBuilder.OutputRoot);

            GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
            if (GUILayout.Button("构建或重建原版地表材质与控制图", GUILayout.Height(38.0f)))
            {
                try
                {
                    CK3TerrainMaterialLibrary library = CK3TerrainMaterialBuilder.Build();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版地表材质与控制图构建完成。\n" +
                        $"材质数量：{library.Materials.Count}\n" +
                        $"控制图尺寸：{library.DetailIndexTexture.width}×{library.DetailIndexTexture.height}\n" +
                        $"输出：{CK3TerrainMaterialBuilder.LibraryPath}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "构建失败，详情请查看 Console。", "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 3　原版高度页表", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", "packed_heightmap.png + indirection_heightmap.png + heightmap.heightmap");
            EditorGUILayout.LabelField("输出", CK3TerrainHeightBuilder.HeightDataPath);
            EditorGUILayout.HelpBox(
                "构建已经确认的 CK3 GPU 高度页表资产；节点 Mesh、Skirt 和 ForceLodLevel 场景构建已在后续阶段接入。",
                MessageType.Info);

            if (GUILayout.Button("构建或重建原版高度页表", GUILayout.Height(38.0f)))
            {
                try
                {
                    CK3TerrainHeightData heightData = CK3TerrainHeightBuilder.Build();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版高度页表构建完成。\n" +
                        $"压缩高度图：{heightData.PackedHeightmapSize.x}×{heightData.PackedHeightmapSize.y} R16\n" +
                        $"间接寻址图：{heightData.IndirectionSize.x}×{heightData.IndirectionSize.y} RGBA32\n" +
                        $"压缩层级：0–{heightData.MaxCompressionLevel}\n" +
                        $"输出：{CK3TerrainHeightBuilder.HeightDataPath}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "高度页表构建失败，详情请查看 Console。", "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 4　原版地形节点网格", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", "CK3 CPdxTerrain 原版顶点与索引生成规则");
            EditorGUILayout.LabelField("输出", CK3TerrainNodeMeshBuilder.MeshPath);
            EditorGUILayout.LabelField("裙边输出", CK3TerrainNodeMeshBuilder.SkirtMeshPath);
            EditorGUILayout.HelpBox(
                "生成原版 33×33 节点网格及闭合 Skirt：主网格 1089 顶点、2048 三角形；裙边 256 顶点，并保存原版 LOD 方向通道。",
                MessageType.Info);

            if (GUILayout.Button("构建或重建原版地形节点与裙边网格", GUILayout.Height(38.0f)))
            {
                try
                {
                    Mesh mesh = CK3TerrainNodeMeshBuilder.Build();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版地形节点网格构建完成。\n" +
                        $"顶点：{mesh.vertexCount}\n" +
                        $"三角形：{mesh.GetIndexCount(0) / 3}\n" +
                        $"裙边：256 顶点，256 个条带三角形\n" +
                        $"输出：{CK3TerrainNodeMeshBuilder.MeshPath}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "节点网格构建失败，详情请查看 Console。", "确定");
                }
            }

            GUI.enabled = true;

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 5　完整世界地形场景", EditorStyles.boldLabel);
            forceLodLevel = EditorGUILayout.IntSlider(
                new GUIContent("原版强制 LOD 层级", "对应 CK3 Terrain.Quadtree.ForceLodLevel；当前用于 Editor 完整世界预览。"),
                forceLodLevel,
                0,
                10);
            EditorGUILayout.HelpBox(
                "读取完整 nodes.dat，按 CK3 原版 ForceLodLevel 路径创建共享节点网格和 Skirt，并在 Scene 非运行模式中显示完整世界。默认第 7 层。",
                MessageType.Info);

            if (GUILayout.Button("构建或重建原版完整世界地形场景", GUILayout.Height(44.0f)))
            {
                try
                {
                    int nodeCount = CK3TerrainWorldBuilder.Build(forceLodLevel);
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"完整世界地形场景构建完成。\n" +
                        $"ForceLodLevel：{forceLodLevel}\n" +
                        $"有效节点：{nodeCount}\n" +
                        $"场景根对象：{CK3TerrainWorldBuilder.RootName}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "完整世界地形场景构建失败，详情请查看 Console。", "确定");
                }
            }

            if (GUILayout.Button("在 Scene 中定位完整世界", GUILayout.Height(30.0f)))
            {
                CK3TerrainWorldBuilder.FocusSceneView();
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 6　省份与法理行政区划", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", "provinces.png + definition.csv + default.map + adjacencies.csv + landed_titles");
            EditorGUILayout.LabelField("省份输出", CK3PoliticalDataBuilder.ProvinceDatabasePath);
            EditorGUILayout.LabelField("法理输出", CK3PoliticalDataBuilder.TitleHierarchyPath);
            EditorGUILayout.HelpBox(
                "保存完整 9216×4608 省份颜色编号图、13270 条省份定义、default.map 原版分类、特殊相邻关系，以及 e/k/d/c/b 法理嵌套和逐省份层级索引。此阶段不把法理层级冒充指定日期的实际统治状态。",
                MessageType.Info);

            if (GUILayout.Button("构建或重建原版省份与法理行政数据", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3PoliticalDataBuildResult result = CK3PoliticalDataBuilder.Build();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"省份与法理行政数据构建完成。\n" +
                        $"省份定义：{result.ProvinceDatabase.Definitions.Length}\n" +
                        $"省份图：{result.ProvinceDatabase.TextureSize.x}×{result.ProvinceDatabase.TextureSize.y}\n" +
                        $"特殊相邻关系：{result.ProvinceDatabase.Adjacencies.Length}\n" +
                        $"法理头衔：{result.TitleHierarchy.Titles.Length}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        "省份与法理行政数据构建失败，详情请查看 Console。",
                        "确定");
                }
            }
            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 7　政治填色与地图模式预览", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("省份间接寻址", CK3PoliticalMapBuilder.ProvinceIdPath);
            EditorGUILayout.LabelField("法理王国颜色表", CK3PoliticalMapBuilder.DeJurePalettePath);
            EditorGUILayout.LabelField("实际领地颜色表", CK3PoliticalMapBuilder.ActualPalettePath);
            EditorGUILayout.LabelField("法理边界距离场", CK3PoliticalMapBuilder.DeJureDistancePath);
            EditorGUILayout.LabelField("实际边界距离场", CK3PoliticalMapBuilder.ActualDistancePath);
            EditorGUILayout.LabelField("法理无水岸距离场", CK3PoliticalMapBuilder.DeJureDistanceWithoutShorelinePath);
            EditorGUILayout.LabelField("实际无水岸距离场", CK3PoliticalMapBuilder.ActualDistanceWithoutShorelinePath);
            EditorGUILayout.HelpBox(
                "按 CK3 的 ProvinceColorIndirectionTexture + 256×256 ProvinceColorTexture 路径构建，并执行原版四倍降采样、Init、16/8/4/2/1 Fill、Finalize 边界距离场流程；同时执行水域 WildCardMask + InitWithWildcards，生成档位 8 后使用的无水岸政治边缘。法理地图使用 dejure_kingdoms；实际地图读取 1066.9.15 的 title history。Play 模式按 F1/F2 切换。",
                MessageType.Info);
            if (GUILayout.Button("构建政治填色并绑定相机与地图模式", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3PoliticalMapBuildResult result = CK3PoliticalMapBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"政治地图构建完成。\n" +
                        $"法理填色省份：{result.DeJureColoredProvinceCount}\n" +
                        $"1066.9.15 实际填色省份：{result.ActualColoredProvinceCount}\n" +
                        "两套原版边界距离场已保存。进入 Play 后使用 WASD/滚轮移动缩放，F1 法理地图，F2 实际地图。",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "政治地图构建失败，详情请查看 Console。", "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 8　原版曲线地图名称", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("字体输入", "Paradox_King_Script.otf / MapFont 50");
            EditorGUILayout.LabelField("名称源", "dejure_kingdoms = 7 / realms = 9");
            EditorGUILayout.LabelField("输出", CK3MapNameSourceBuilder.SourceDataPath);
            EditorGUILayout.HelpBox(
                "同时持久化法理王国名称区域与 1066.9.15 实际 Realm 区域。实际 Realm 直接复用政治地图的原版头衔历史、holder 与 liege 链，并使用顶级 Realm 的主头衔/历史名称；两种模式共用已确认的原版曲线与逐字网格算法。",
                MessageType.Info);
            if (GUILayout.Button("构建 F1/F2 地图名称源与字体图集", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3MapNameSourceBuildResult result = CK3MapNameSourceBuilder.BuildDeJureKingdomSources();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版地图名称源构建完成。\n连通名称区域：{result.RegionCount}\n字体字符：{result.GlyphCount}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "地图名称源构建失败，详情请查看 Console。", "确定");
                }
            }
            EditorGUILayout.HelpBox(
                "曲线生成已改为 countryname.cpp 的区域像素统计、两条回归轴、-5…+5 平行测试线、最长连续区域段、原版缩放/字距、五控制点与两段 Catmull-Rom。当前 Shader 只开放证据完整的平面地图名称分支。",
                MessageType.Info);
            if (GUILayout.Button("构建并绑定 F1/F2 原版曲线名称", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3MapNameMeshBuildResult result = CK3MapNameMeshBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版曲线地图名称已生成并绑定。\n名称：{result.NameCount}\n字形：{result.GlyphCount}\nScene 与 Game 使用同一持久网格。",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "曲线地图名称生成失败，详情请查看 Console。", "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 9　原版海洋水面", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("几何", "jomini_water.cpp：完整世界四顶点平面");
            EditorGUILayout.LabelField("水位", "00_defines.txt：WATERLEVEL = 3");
            EditorGUILayout.LabelField("输出", CK3WaterSurfaceBuilder.OutputRoot);
            EditorGUILayout.HelpBox(
                "当前按钮只构建证据闭合的普通海洋与 waterLowSpec 基线：持久化四角海面、原版 BC7 水色图、真实地形深度和岸边淡出。高位独立湖泊 Mesh 尚无 A 级生成证据，不在本阶段猜测生成。",
                MessageType.Info);
            if (GUILayout.Button("构建并绑定原版完整世界海洋水面", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3WaterSurfaceBuildResult result = CK3WaterSurfaceBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版完整世界海洋水面已生成并绑定。\n" +
                        $"网格顶点：{result.Mesh.vertexCount}\n" +
                        $"水色纹理：{result.WaterColorTexture.width}×{result.WaterColorTexture.height} BC7\n" +
                        "Scene 与 Game 使用同一持久化资产。",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "海洋水面构建失败，详情请查看 Console。", "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 10　原版位图河网", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", "game/map_data/rivers.png（9216×4608 索引色）");
            EditorGUILayout.LabelField("输出", CK3RiverNetworkBuilder.RiverDataPath);
            EditorGUILayout.HelpBox(
                "本按钮执行已经由原版数据与 ck3.exe 定点反编译共同确认的河网阶段：" +
                "按左、右、上、下四邻域追踪 0/1/2 源点，保存父河关系、原版像素路径和宽度。" +
                "曲线平滑、细分与河面顶点阶段在证据闭合后由同一阶段继续构建，不使用 LineRenderer 替代。",
                MessageType.Info);
            if (GUILayout.Button("构建或重建原版位图河网数据", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3RiverNetworkBuildResult result = CK3RiverNetworkBuilder.Build();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版位图河网数据构建完成。\n" +
                        $"主河流：{result.MainRiverCount}\n" +
                        $"支流：{result.SecondaryRiverCount}\n" +
                        $"原版忽略的未连接支流源点：{result.IgnoredSecondarySourceCount}\n" +
                        $"河流记录：{result.Data.Rivers.Length}\n" +
                        $"路径点：{result.Data.Points.Length}\n" +
                        $"输出：{CK3RiverNetworkBuilder.RiverDataPath}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        "原版位图河网数据构建失败，详情请查看 Console。",
                        "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 11　原版河面网格", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("输入", CK3RiverNetworkBuilder.RiverDataPath);
            EditorGUILayout.LabelField("网格输出", CK3RiverSurfaceBuilder.MeshPath);
            EditorGUILayout.LabelField("材质输出", CK3RiverSurfaceBuilder.MaterialPath);
            EditorGUILayout.HelpBox(
                "使用原版两轮移动平均、Catmull-Rom 曲线、自适应角度细分、13 级宽度与河带横向展开，" +
                "生成完整世界的持久化河面 Mesh，并直接绑定到当前 Scene。",
                MessageType.Info);
            if (GUILayout.Button("构建或重建原版河面并绑定场景", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3RiverSurfaceBuildResult result = CK3RiverSurfaceBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版全世界河面构建完成。\n" +
                        $"河流记录：{result.RiverCount}\n" +
                        $"曲线采样点：{result.CurvePointCount}\n" +
                        $"河面顶点：{result.Mesh.vertexCount}\n" +
                        $"输出：{CK3RiverSurfaceBuilder.MeshPath}",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        "原版河面构建失败，详情请查看 Console。",
                        "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 12　原版全世界树木与城堡", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("树木实例", "原版 map_object_data/generated 完整世界坐标");
            EditorGUILayout.LabelField("模型输入", "原版 pdxasset .mesh：p / n / ta / u0 / tri");
            EditorGUILayout.LabelField("输出", CK3DecorationBuilder.OutputRoot);
            EditorGUILayout.HelpBox(
                "读取 map_object_data/generated 中全部树木生成器、原版 LOD0 Mesh、纹理与约 45 万条世界实例；" +
                "城堡位置来自 building_locators.txt 与 history/provinces 中基础 castle_holding 的交集。" +
                "全部装饰由一个根对象按空间分块、视锥和距离裁剪后 GPU Instancing 绘制，不创建数十万 GameObject。" +
                "同时导入 cliff_big_01 山石原型；原版 cliffs_rock.txt 对该原型的实例数为 0，因此本阶段不捏造山石坐标。" +
                "Unity GPU Instancing 和 URP/Lit 仅是明确隔离的预览载体，后续再由原版 tree.shader/pdxmesh.shader 移植替换。",
                MessageType.Info);
            if (GUILayout.Button("构建并绑定全世界树木与城堡", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3DecorationBuildResult result = CK3DecorationBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"原版近景装饰首阶段构建完成。\n" +
                        $"树木原型：{result.TreePrototypeCount}\n" +
                        $"树木实例：{result.TreeInstanceCount:N0}\n" +
                        $"建筑原型：{result.BuildingPrototypeCount}\n" +
                        $"城堡实例：{result.BuildingInstanceCount:N0}\n" +
                        $"山石原型：{result.RockPrototype.name}\n" +
                        "Scene 与 Game 由同一持久资产显示。",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        "原版近景装饰构建失败，详情请查看 Console。",
                        "确定");
                }
            }

            EditorGUILayout.Space(12.0f);
            EditorGUILayout.LabelField("阶段 13　原版桌面、平面地图与世界外侧", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("桌面输入", "game/gfx/models/tabletop（默认西式四组）");
            EditorGUILayout.LabelField("平面地图输入", "flatmap.dds + paper_tear_mask.dds");
            EditorGUILayout.LabelField("世界外侧输入", "surround_mask.dds + surround_fade.dds");
            EditorGUILayout.LabelField("输出", CK3TabletopFlatMapBuilder.OutputRoot);
            EditorGUILayout.HelpBox(
                "按原版 map_table_western.txt 的四组实体 Transform 在 Scene 中保存完整多子网格桌面；" +
                "Terrain 与 Water 共用原版 Flat Map、20→21 档压平和 paper_transition.fxh 撕纸过渡；" +
                "世界外侧使用 surroundmap.shader 已确认的 flat pass。桌布动画、蜡烛火焰和动态云属于下一阶段。",
                MessageType.Info);
            if (GUILayout.Button("构建并绑定原版桌面、平面地图与世界外侧", GUILayout.Height(44.0f)))
            {
                try
                {
                    CK3TabletopFlatMapBuildResult result = CK3TabletopFlatMapBuilder.BuildAndBind();
                    EditorUtility.DisplayDialog(
                        "CK3 地图构建器",
                        $"阶段 13 构建完成。\n桌面对象：{result.TabletopObjectCount}\n" +
                        $"Flat Map：{result.FlatMap.width}×{result.FlatMap.height}\n" +
                        "Scene 已绑定桌面、Terrain/Water 平面地图过渡与 Surround Map。",
                        "确定");
                }
                catch (Exception exception) when (!(exception is ExitGUIException))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("CK3 地图构建器", "阶段 13 构建失败，请查看 Console。", "确定");
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}

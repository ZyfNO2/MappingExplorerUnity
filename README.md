# Mapping Explorer Unity

一个基于 Unity 的点云可视化与交互工具，用于加载、渲染和操作 PLY 格式的点云数据。

## 功能特性

- **点云加载与渲染**：支持 PLY 格式点云文件的加载和高效渲染
- **实时点云拾取**：通过鼠标点击选择点云中的点，获取坐标和颜色信息
- **相机同步**：Scene 视图与 Game 视图的相机实时同步
- **点云过滤**：支持按坐标范围过滤显示点云
- **自定义着色器**：使用 GPU 实例化技术高效渲染大量点云数据

## 项目结构

```
Assets/
├── Scripts/
│   ├── PointCloudRenderer.cs      # 点云渲染器，负责加载和显示点云
│   ├── PointCloudPicker.cs        # 点云拾取器，处理鼠标交互
│   ├── CameraSync.cs              # 相机同步工具
│   └── TestObjectGenerator.cs     # 测试对象生成器
├── Shaders/
│   └── PointCloudShader.shader    # 点云渲染着色器
├── Data/
│   └── *.ply, *.csv               # 点云数据文件
└── com.IvanMurzak/
    └── AI Game Dev Installer/     # AI Game Dev 安装器工具
```

## 核心组件

### PointCloudRenderer
点云渲染组件，主要功能：
- 从 PLY 文件加载点云数据
- 使用 Mesh 和 GPU 实例化渲染点云
- 支持点大小、颜色自定义
- 支持坐标范围过滤

### PointCloudPicker
点云拾取组件，主要功能：
- 鼠标点击拾取点云中的点
- 显示拾取点的坐标和颜色
- 支持可视化调试（Gizmos）

### CameraSync
相机同步组件，主要功能：
- 实时同步 Scene 视图和 Game 视图的相机
- 支持位置和旋转同步
- 支持相机参数同步（FOV、裁剪面等）

## 使用方法

1. **加载点云**：
   - 将 PLY 文件放入 `Assets/Data/` 目录
   - 在 PointCloudRenderer 组件中设置文件路径
   - 运行场景即可看到点云

2. **点云拾取**：
   - 确保场景中有 PointCloudPicker 组件
   - 运行场景后，使用鼠标左键点击点云
   - 在 Console 中查看拾取的点信息

3. **相机同步**：
   - 添加 CameraSync 组件到场景
   - 设置要同步的游戏相机
   - 在编辑器中移动 Scene 视图相机，Game 视图会同步跟随

## 依赖

- Unity 2022.3 LTS 或更高版本
- 支持 GPU 实例化的显卡

## 许可证

[LICENSE](LICENSE)

## 作者

ZyfNO2

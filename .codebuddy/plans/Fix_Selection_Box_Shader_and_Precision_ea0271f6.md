---
name: Fix Selection Box Shader and Precision
overview: 修复选择框的Shader洋红色错误，并改进点云裁剪算法为精确逐点筛选
todos:
  - id: fix-shader-error
    content: 使用[skill:unity-material]修复PointCloudRegionSelector.cs中的shader错误，替换为URP兼容的透明shader
    status: completed
  - id: implement-precise-filter
    content: 在PointCloudManager.cs中实现精确的逐点裁剪算法FilterByBoundsPrecise
    status: completed
    dependencies:
      - fix-shader-error
  - id: integrate-filter-logic
    content: 修改SelectionBoxController和PointCloudRegionSelector，使用新的精确裁剪方法
    status: completed
    dependencies:
      - implement-precise-filter
  - id: test-functionality
    content: 使用[skill:unity-validation]验证修复后的功能，测试shader显示和裁剪精度
    status: completed
    dependencies:
      - integrate-filter-logic
  - id: update-scene-components
    content: 使用[mcp:unityMCP]更新PlyTest场景中的组件绑定和引用关系
    status: completed
    dependencies:
      - test-functionality
---

## 问题修复概述

修复点云选择功能的两个关键问题：

1. **Shader洋红色错误**：创建的选择框显示洋红色（shader加载失败）
2. **裁剪不准确**：基于网格单元的筛选算法导致不精确的结果

## 核心修复内容

### 问题1：Shader错误修复

- **现状**：使用"Transparent/Diffuse" shader在URP管线中不可用
- **修复**：替换为URP兼容的透明shader（"Universal Render Pipeline/Lit"）
- **影响文件**：`PointCloudRegionSelector.cs`第247行

### 问题2：精确裁剪算法

- **现状**：基于网格单元筛选（只要网格与立方体相交就显示整个单元）
- **修复**：实现逐点精确筛选，检查每个点的XYZ坐标是否在立方体空间范围内
- **实现方案**：
- 获取立方体的世界空间边界（Bounds）
- 遍历所有点云数据，检查point.position是否在边界内
- 重新构建只包含内部点的Mesh
- 保留原始数据，支持撤销/恢复
- **影响文件**：`PointCloudManager.cs`（新增精确筛选方法）

### 视觉与交互效果

- 立方体显示为半透明的蓝色（RGBA: 0.3, 0.6, 1.0, 0.3）
- 边框显示为青色线框
- 拖拽时实时预览筛选效果
- 只显示位于立方体内部的点云，外部点云被隐藏

## 技术方案

### Shader修复

**原因分析**：项目使用Universal Render Pipeline (URP)，"Transparent/Diffuse"是Built-in管线的shader，在URP中不可用导致洋红色错误

**解决方案**：

```
// 使用URP Lit shader并设置为透明模式
Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
mat.SetFloat("_Surface", 1); // 1 = Transparent
mat.SetFloat("_Blend", 0);   // 0 = Alpha
mat.color = new Color(0.3f, 0.6f, 1.0f, 0.3f);
mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
```

### 精确裁剪算法

**核心思路**：

1. 计算立方体的世界空间边界范围
2. 遍历点云数据，筛选出边界内的点
3. 基于筛选后的点重新生成Mesh
4. 利用现有空间分桶结构优化性能

**算法优化**：

- 先使用空间网格快速排除不可能的单元（粗筛）
- 在候选单元内逐点精确检查（精筛）
- 避免遍历所有点，复杂度从O(n)降低到O(k + m)

**数据结构**：

- 保留原始pointCloudData（用于恢复）
- 创建filteredPointIndices存储筛选后的点索引
- 基于filteredPointIndices重建Mesh

### 兼容性保证

- 保留原有FilterByBounds方法（用于快速预览）
- 新增FilterByBoundsPrecise方法（用于精确裁剪）
- 提供ClearFilter方法恢复所有点云

## Agent Extensions

### MCP

- **unityMCP**: Unity场景操作和组件管理
- 用途：在PlyTest场景中添加/修改组件，设置材质属性
- 预期结果：PointCloudRegionSelector组件正确配置，选择框使用URP shader

### Unity Skills

- **unity-material**: 创建和配置URP材质
- 用途：创建兼容URP的透明材质球
- 预期结果：材质使用"Universal Render Pipeline/Lit" shader，透明度0.3，颜色蓝色

- **unity-validation**: 验证场景和脚本
- 用途：验证修改后的功能完整性
- 预期结果：无编译错误，选择框正常显示，裁剪功能精确工作
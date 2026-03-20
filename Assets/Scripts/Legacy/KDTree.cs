/*
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// KD树 - 用于高效的空间查询（最近邻、范围搜索）
/// 比简单遍历快O(log n)倍
/// </summary>
public class KDTree
{
    private KDNode root;
    private List<Vector3> points;
    private int leafSize;
    
    /// <summary>
    /// KD树节点
    /// </summary>
    private class KDNode
    {
        public int pointIndex;      // 分割点索引
        public int splitDimension;  // 分割维度 (0=x, 1=y, 2=z)
        public KDNode left;         // 左子树
        public KDNode right;        // 右子树
        public Bounds bounds;       // 节点边界
        public bool isLeaf;         // 是否为叶子节点
        public List<int> indices;   // 叶子节点包含的点索引
        
        public KDNode(Bounds b)
        {
            bounds = b;
            isLeaf = false;
            indices = new List<int>();
        }
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="points">点集</param>
    /// <param name="leafSize">叶子节点最大点数</param>
    public KDTree(List<Vector3> points, int leafSize = 32)
    {
        this.points = points;
        this.leafSize = leafSize;
        
        if (points.Count == 0) return;
        
        // 计算整体边界
        Bounds totalBounds = new Bounds(points[0], Vector3.zero);
        for (int i = 1; i < points.Count; i++)
        {
            totalBounds.Encapsulate(points[i]);
        }
        
        // 创建索引列表
        List<int> indices = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            indices.Add(i);
        }
        
        // 构建树
        root = BuildTree(indices, totalBounds, 0);
        
        Debug.Log($"[KDTree] Built tree with {points.Count} points, leaf size {leafSize}");
    }
    
    /// <summary>
    /// 递归构建KD树
    /// </summary>
    private KDNode BuildTree(List<int> indices, Bounds bounds, int depth)
    {
        KDNode node = new KDNode(bounds);
        
        // 如果点数少于leafSize，创建叶子节点
        if (indices.Count <= leafSize)
        {
            node.isLeaf = true;
            node.indices = new List<int>(indices);
            return node;
        }
        
        // 选择分割维度（循环选择x, y, z）
        int splitDim = depth % 3;
        node.splitDimension = splitDim;
        
        // 按分割维度排序
        indices.Sort((a, b) =>
        {
            float valA = GetDimensionValue(points[a], splitDim);
            float valB = GetDimensionValue(points[b], splitDim);
            return valA.CompareTo(valB);
        });
        
        // 选择中位数作为分割点
        int medianIdx = indices.Count / 2;
        node.pointIndex = indices[medianIdx];
        
        // 分割点集
        List<int> leftIndices = new List<int>();
        List<int> rightIndices = new List<int>();
        
        for (int i = 0; i < indices.Count; i++)
        {
            if (i < medianIdx)
                leftIndices.Add(indices[i]);
            else if (i > medianIdx)
                rightIndices.Add(indices[i]);
        }
        
        // 计算子边界
        float splitValue = GetDimensionValue(points[node.pointIndex], splitDim);
        
        Bounds leftBounds = new Bounds(bounds.center, bounds.size);
        Bounds rightBounds = new Bounds(bounds.center, bounds.size);
        
        // 调整边界
        Vector3 leftMax = leftBounds.max;
        Vector3 rightMin = rightBounds.min;
        
        switch (splitDim)
        {
            case 0: // x
                leftMax.x = splitValue;
                rightMin.x = splitValue;
                break;
            case 1: // y
                leftMax.y = splitValue;
                rightMin.y = splitValue;
                break;
            case 2: // z
                leftMax.z = splitValue;
                rightMin.z = splitValue;
                break;
        }
        
        leftBounds.max = leftMax;
        rightBounds.min = rightMin;
        
        // 递归构建子树
        if (leftIndices.Count > 0)
            node.left = BuildTree(leftIndices, leftBounds, depth + 1);
        
        if (rightIndices.Count > 0)
            node.right = BuildTree(rightIndices, rightBounds, depth + 1);
        
        return node;
    }
    
    /// <summary>
    /// 获取向量的某个维度值
    /// </summary>
    private float GetDimensionValue(Vector3 v, int dim)
    {
        switch (dim)
        {
            case 0: return v.x;
            case 1: return v.y;
            case 2: return v.z;
            default: return v.x;
        }
    }
    
    /// <summary>
    /// 查找最近邻点
    /// </summary>
    /// <param name="queryPoint">查询点</param>
    /// <param name="k">返回最近k个邻居</param>
    /// <returns>最近邻点索引列表</returns>
    public List<int> FindNearestNeighbors(Vector3 queryPoint, int k)
    {
        List<(int index, float dist)> nearest = new List<(int, float)>();
        
        if (root == null) return new List<int>();
        
        FindNearestRecursive(root, queryPoint, k, nearest);
        
        // 提取索引
        List<int> result = new List<int>();
        foreach (var item in nearest)
        {
            result.Add(item.index);
        }
        
        return result;
    }
    
    /// <summary>
    /// 递归查找最近邻
    /// </summary>
    private void FindNearestRecursive(KDNode node, Vector3 queryPoint, int k, List<(int, float)> nearest)
    {
        if (node == null) return;
        
        // 如果是叶子节点，检查所有点
        if (node.isLeaf)
        {
            foreach (int idx in node.indices)
            {
                float dist = Vector3.Distance(queryPoint, points[idx]);
                InsertNearest(nearest, idx, dist, k);
            }
            return;
        }
        
        // 计算当前点的距离
        float currentDist = Vector3.Distance(queryPoint, points[node.pointIndex]);
        InsertNearest(nearest, node.pointIndex, currentDist, k);
        
        // 决定搜索顺序
        float splitValue = GetDimensionValue(points[node.pointIndex], node.splitDimension);
        float queryValue = GetDimensionValue(queryPoint, node.splitDimension);
        
        KDNode first = queryValue < splitValue ? node.left : node.right;
        KDNode second = queryValue < splitValue ? node.right : node.left;
        
        // 先搜索近的子树
        if (first != null)
        {
            FindNearestRecursive(first, queryPoint, k, nearest);
        }
        
        // 检查是否需要搜索远的子树
        if (second != null)
        {
            float bestDist = nearest.Count < k ? float.MaxValue : nearest[nearest.Count - 1].Item2;
            float diff = Mathf.Abs(queryValue - splitValue);
            
            // 如果分割平面距离小于当前最佳距离，需要搜索另一子树
            if (diff < bestDist)
            {
                FindNearestRecursive(second, queryPoint, k, nearest);
            }
        }
    }
    
    /// <summary>
    /// 插入最近邻列表（保持排序）
    /// </summary>
    private void InsertNearest(List<(int index, float dist)> nearest, int index, float dist, int k)
    {
        // 如果列表未满，直接插入
        if (nearest.Count < k)
        {
            nearest.Add((index, dist));
            // 排序
            nearest.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return;
        }
        
        // 如果比最远的还近，替换
        if (dist < nearest[nearest.Count - 1].Item2)
        {
            nearest[nearest.Count - 1] = (index, dist);
            // 重新排序
            nearest.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        }
    }
    
    /// <summary>
    /// 范围搜索 - 查找半径内的所有点
    /// </summary>
    /// <param name="center">中心点</param>
    /// <param name="radius">半径</param>
    /// <returns>范围内点索引列表</returns>
    public List<int> RangeSearch(Vector3 center, float radius)
    {
        List<int> result = new List<int>();
        
        if (root == null) return result;
        
        RangeSearchRecursive(root, center, radius, result);
        
        return result;
    }
    
    /// <summary>
    /// 递归范围搜索
    /// </summary>
    private void RangeSearchRecursive(KDNode node, Vector3 center, float radius, List<int> result)
    {
        if (node == null) return;
        
        // 检查节点边界是否与搜索范围相交
        if (!node.bounds.Intersects(new Bounds(center, Vector3.one * radius * 2)))
        {
            return;
        }
        
        // 如果是叶子节点，检查所有点
        if (node.isLeaf)
        {
            foreach (int idx in node.indices)
            {
                float dist = Vector3.Distance(center, points[idx]);
                if (dist <= radius)
                {
                    result.Add(idx);
                }
            }
            return;
        }
        
        // 检查当前点
        float currentDist = Vector3.Distance(center, points[node.pointIndex]);
        if (currentDist <= radius)
        {
            result.Add(node.pointIndex);
        }
        
        // 递归搜索子树
        RangeSearchRecursive(node.left, center, radius, result);
        RangeSearchRecursive(node.right, center, radius, result);
    }
    
    /// <summary>
    /// 获取点数量
    /// </summary>
    public int Count
    {
        get { return points != null ? points.Count : 0; }
    }
}
*/

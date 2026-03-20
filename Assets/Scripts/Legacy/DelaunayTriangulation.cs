/*
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 改进的Delaunay三角剖分算法
/// 使用局部平面投影和角度约束
/// </summary>
public static class DelaunayTriangulation
{
    /// <summary>
    /// 对点云进行Delaunay三角剖分
    /// </summary>
    public static List<int> Triangulate(
        List<PointCloudManager.PointData> points, 
        KDTree kdTree, 
        int neighborCount, 
        float maxEdgeLength)
    {
        List<int> triangles = new List<int>();
        
        if (points.Count < 3)
        {
            Debug.LogWarning("[DelaunayTriangulation] Not enough points");
            return triangles;
        }
        
        Debug.Log($"[DelaunayTriangulation] Starting with {points.Count} points...");
        
        HashSet<(int, int, int)> triangleSet = new HashSet<(int, int, int)>();
        
        // 对每个点进行局部三角剖分
        for (int i = 0; i < points.Count; i++)
        {
            // 查找邻居
            List<int> neighbors = kdTree.FindNearestNeighbors(points[i].position, neighborCount);
            
            // 创建局部三角形
            List<int> localTriangles = CreateLocalTrianglesImproved(i, neighbors, points, maxEdgeLength);
            
            // 添加到集合
            for (int t = 0; t < localTriangles.Count; t += 3)
            {
                int a = localTriangles[t];
                int b = localTriangles[t + 1];
                int c = localTriangles[t + 2];
                
                // 排序去重
                int[] sorted = new int[] { a, b, c };
                Array.Sort(sorted);
                
                var key = (sorted[0], sorted[1], sorted[2]);
                if (!triangleSet.Contains(key))
                {
                    triangleSet.Add(key);
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
            }
            
            // 进度输出
            if ((i + 1) % 5000 == 0)
            {
                Debug.Log($"[DelaunayTriangulation] Processed {i + 1}/{points.Count} points...");
            }
        }
        
        Debug.Log($"[DelaunayTriangulation] Complete. Generated {triangles.Count / 3} triangles");
        
        return triangles;
    }
    
    /// <summary>
    /// 改进的局部三角形创建 - 使用局部平面投影
    /// </summary>
    private static List<int> CreateLocalTrianglesImproved(
        int centerIdx, 
        List<int> neighbors, 
        List<PointCloudManager.PointData> points, 
        float maxEdgeLength)
    {
        List<int> triangles = new List<int>();
        
        if (neighbors.Count < 2) return triangles;
        
        Vector3 center = points[centerIdx].position;
        
        // 计算局部法线（使用PCA或简单平均）
        Vector3 normal = EstimateLocalNormal(centerIdx, neighbors, points);
        
        // 创建局部坐标系
        Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
        if (tangent.magnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.right).normalized;
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
        
        // 收集有效邻居并投影到局部平面
        List<(int idx, float angle, float dist, Vector3 projected)> validNeighbors = 
            new List<(int, float, float, Vector3)>();
        
        foreach (int neighborIdx in neighbors)
        {
            Vector3 dir = points[neighborIdx].position - center;
            float dist = dir.magnitude;
            
            // 严格距离过滤
            if (dist > maxEdgeLength || dist < 0.001f) continue;
            
            // 投影到切平面
            Vector3 projected = new Vector3(
                Vector3.Dot(dir, tangent),
                Vector3.Dot(dir, bitangent),
                Vector3.Dot(dir, normal)
            );
            
            // 检查投影距离（过滤离平面太远的点）
            if (Mathf.Abs(projected.z) > maxEdgeLength * 0.3f) continue;
            
            // 计算角度
            float angle = Mathf.Atan2(projected.y, projected.x);
            
            validNeighbors.Add((neighborIdx, angle, dist, projected));
        }
        
        if (validNeighbors.Count < 2) return triangles;
        
        // 按角度排序
        validNeighbors.Sort((a, b) => a.angle.CompareTo(b.angle));
        
        // 创建三角形，添加角度约束
        for (int i = 0; i < validNeighbors.Count; i++)
        {
            int nextIdx = (i + 1) % validNeighbors.Count;
            
            int a = centerIdx;
            int b = validNeighbors[i].idx;
            int c = validNeighbors[nextIdx].idx;
            
            // 检查角度差（避免创建太扁的三角形）
            float angleDiff = Mathf.Abs(validNeighbors[nextIdx].angle - validNeighbors[i].angle);
            if (angleDiff > Mathf.PI) angleDiff = 2 * Mathf.PI - angleDiff;
            
            // 过滤小角度（小于15度）
            if (angleDiff < Mathf.PI / 12) continue;
            
            // 检查边长
            float ab = Vector3.Distance(points[a].position, points[b].position);
            float ac = Vector3.Distance(points[a].position, points[c].position);
            float bc = Vector3.Distance(points[b].position, points[c].position);
            
            if (ab > maxEdgeLength || ac > maxEdgeLength || bc > maxEdgeLength) continue;
            
            // 检查边长比例（避免太扁的三角形）
            float maxEdge = Mathf.Max(ab, ac, bc);
            float minEdge = Mathf.Min(ab, ac, bc);
            if (maxEdge / minEdge > 5) continue; // 边长比例不超过5:1
            
            // 检查面积
            float area = CalculateTriangleArea(points[a].position, points[b].position, points[c].position);
            if (area < 0.00001f) continue;
            
            // 检查法线一致性
            Vector3 triNormal = CalculateNormal(points[a].position, points[b].position, points[c].position);
            if (Vector3.Dot(triNormal, normal) < 0.5f) continue; // 法线方向差异太大
            
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
        
        return triangles;
    }
    
    /// <summary>
    /// 估计局部法线（使用PCA简化版）
    /// </summary>
    private static Vector3 EstimateLocalNormal(int centerIdx, List<int> neighbors, List<PointCloudManager.PointData> points)
    {
        Vector3 center = points[centerIdx].position;
        
        // 使用邻居计算协方差矩阵的特征向量
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        foreach (int idx in neighbors)
        {
            if (idx == centerIdx) continue;
            sum += points[idx].position - center;
            count++;
        }
        
        if (count == 0) return Vector3.up;
        
        // 简单的法线估计：平均方向垂直于局部平面
        Vector3 avgDir = sum / count;
        
        // 使用中心点的法线（如果有）或计算
        if (points[centerIdx].normal != Vector3.zero)
        {
            return points[centerIdx].normal.normalized;
        }
        
        // 简化：返回与平均方向垂直的向量
        Vector3 normal = Vector3.Cross(avgDir, Vector3.up).normalized;
        if (normal.magnitude < 0.1f)
            normal = Vector3.Cross(avgDir, Vector3.right).normalized;
        
        return normal.magnitude > 0.1f ? normal : Vector3.up;
    }
    
    /// <summary>
    /// 计算三角形面积
    /// </summary>
    private static float CalculateTriangleArea(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        return Vector3.Cross(ab, ac).magnitude * 0.5f;
    }
    
    /// <summary>
    /// 计算三角形法线
    /// </summary>
    private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        return Vector3.Cross(ab, ac).normalized;
    }
    
    /// <summary>
    /// 优化三角形网格
    /// </summary>
    public static List<int> OptimizeTriangles(List<int> triangles, List<PointCloudManager.PointData> points, float maxEdgeLength)
    {
        List<int> optimized = new List<int>();
        HashSet<(int, int, int)> unique = new HashSet<(int, int, int)>();
        
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            
            // 去重
            int[] sorted = new int[] { a, b, c };
            Array.Sort(sorted);
            var key = (sorted[0], sorted[1], sorted[2]);
            
            if (unique.Contains(key)) continue;
            unique.Add(key);
            
            // 检查边长
            float ab = Vector3.Distance(points[a].position, points[b].position);
            float ac = Vector3.Distance(points[a].position, points[c].position);
            float bc = Vector3.Distance(points[b].position, points[c].position);
            
            if (ab > maxEdgeLength || ac > maxEdgeLength || bc > maxEdgeLength) continue;
            
            // 检查面积
            float area = CalculateTriangleArea(points[a].position, points[b].position, points[c].position);
            if (area < 0.00001f) continue;
            
            optimized.Add(a);
            optimized.Add(b);
            optimized.Add(c);
        }
        
        Debug.Log($"[DelaunayTriangulation] Optimized: {triangles.Count / 3} -> {optimized.Count / 3} triangles");
        
        return optimized;
    }
}
*/

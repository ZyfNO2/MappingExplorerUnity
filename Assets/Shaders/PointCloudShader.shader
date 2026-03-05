Shader "Custom/PointCloudShader"
{
    Properties
    {
        _PointSize("Point Size", Range(0.01, 1.0)) = 0.1
        _PointWidth("Point Width", Range(0.1, 5.0)) = 1.0
        _PointHeight("Point Height", Range(0.1, 5.0)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            
            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };
            
            struct v2g
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };
            
            struct g2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            float _PointSize;
            float _PointWidth;
            float _PointHeight;
            
            v2g vert (appdata v)
            {
                v2g o;
                o.vertex = v.vertex;
                o.color = v.color;
                return o;
            }
            
            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float4 pos = input[0].vertex;
                float4 color = input[0].color;
                
                // 计算世界空间位置
                float4 worldPos = mul(unity_ObjectToWorld, pos);
                
                // 计算裁剪空间位置
                float4 clipPos = UnityObjectToClipPos(pos);
                
                // 计算屏幕空间中的点大小（等距效果）
                float screenSize = _PointSize * 0.01;
                float halfWidth = screenSize * _PointWidth;
                float halfHeight = screenSize * _PointHeight;
                
                // 计算视图空间中的方向向量
                float3 viewUp = float3(0, 1, 0);
                float3 viewRight = normalize(cross(viewUp, normalize(worldPos.xyz - _WorldSpaceCameraPos.xyz)));
                viewUp = normalize(cross(viewRight, normalize(worldPos.xyz - _WorldSpaceCameraPos.xyz)));
                
                // 转换方向向量到裁剪空间
                float3 clipUp = mul((float3x3)UNITY_MATRIX_P, viewUp);
                float3 clipRight = mul((float3x3)UNITY_MATRIX_P, viewRight);
                
                // 计算裁剪空间中的偏移量
                float4 offsetRight = float4(clipRight * halfWidth, 0);
                float4 offsetUp = float4(clipUp * halfHeight, 0);
                
                // 计算四个顶点的裁剪空间位置
                float4 v0 = clipPos - offsetRight;
                float4 v1 = clipPos + offsetRight;
                float4 v2 = clipPos - offsetRight + offsetUp;
                float4 v3 = clipPos + offsetRight + offsetUp;
                
                g2f o;
                o.color = color;
                
                o.vertex = v0;
                triStream.Append(o);
                
                o.vertex = v1;
                triStream.Append(o);
                
                o.vertex = v2;
                triStream.Append(o);
                
                o.vertex = v3;
                triStream.Append(o);
            }
            
            fixed4 frag (g2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
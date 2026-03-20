Shader "Custom/PointCloud"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 0.05
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 1
        _Color ("Default Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float size : PSIZE;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            float _PointSize;
            float _UseVertexColor;
            float4 _Color;
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 使用顶点颜色或默认颜色
                if (_UseVertexColor > 0.5)
                    o.color = v.color;
                else
                    o.color = _Color;
                
                // 设置点大小
                o.size = _PointSize * 100; // 缩放因子
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return i.color;
            }
            ENDCG
        }
    }
}

// AdvancedInvert.hlsl

// 参数将从C#代码传入，注册到常量寄存器c0
// c0.x = Gamma
// c0.y = Contrast
// c0.z = Saturation
float4 params : register(c0);

sampler2D input : register(S0);

// 用于计算亮度的标准权重
const float3 LuminanceWeights = float3(0.299, 0.587, 0.114);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    // 1. 获取原始颜色
    float4 color = tex2D(input, uv);
    
    // 2. 简单线性反转
    float3 invertedColor = 1.0 - color.rgb;
    
    // 3. 调整对比度 (Contrast)
    //    - 创建一个中间灰度值（0.5）
    //    - 根据Contrast参数将颜色推向或拉离这个中间值
    float3 contrastedColor = 0.5 + params.y * (invertedColor - 0.5);
    
    // 4. 调整饱和度 (Saturation)
    //    - 计算反转后颜色的灰度版本
    float gray = dot(contrastedColor, LuminanceWeights);
    float3 grayColor = float3(gray, gray, gray);
    //    - 根据Saturation参数在原始颜色和灰度颜色之间插值
    float3 saturatedColor = lerp(grayColor, contrastedColor, params.z);
    
    // 5. 应用Gamma校正
    //    - 这是最后一步，用于调整整体亮度
    float3 finalColor = pow(saturatedColor, params.x);

    return float4(finalColor, color.a);
}
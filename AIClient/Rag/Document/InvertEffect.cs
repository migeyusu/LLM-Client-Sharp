using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace LLMClient.Rag.Document;

public class InvertEffect : ShaderEffect
{
     // 1. Input属性，用于接收图像画刷
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty("Input", typeof(InvertEffect), 0);

        // 2. 用于发送到着色器c0寄存器的参数属性。这是C#和HLSL沟通的真正桥梁。
        //    我们将其设为私有，因为外部用户不应直接操作它。
        private static readonly DependencyProperty ShaderParametersProperty =
            DependencyProperty.Register("ShaderParameters", typeof(Point4D), typeof(InvertEffect),
                new UIPropertyMetadata(new Point4D(1.0, 1.0, 1.0, 0.0), PixelShaderConstantCallback(0)));

        // 3. 暴露给XAML绑定的公共属性
        public static readonly DependencyProperty GammaProperty =
            DependencyProperty.Register("Gamma", typeof(double), typeof(InvertEffect),
                new UIPropertyMetadata(1.0, OnParameterChanged));

        public static readonly DependencyProperty ContrastProperty =
            DependencyProperty.Register("Contrast", typeof(double), typeof(InvertEffect),
                new UIPropertyMetadata(1.0, OnParameterChanged));

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register("Saturation", typeof(double), typeof(InvertEffect),
                new UIPropertyMetadata(1.0, OnParameterChanged));
        
        // 构造函数
        public InvertEffect()
        {
            var pixelShader = new PixelShader
            {
                UriSource = new Uri("pack://application:,,,/LLMClient;component/Resources/Shaders/Invert.ps", UriKind.Absolute)
            };
            this.PixelShader = pixelShader;

            // 必须初始化所有属性，以确保首次加载时值被传递
            UpdateShaderValue(InputProperty);
            UpdateShaderParameters(); // 关键：调用我们的新方法来初始化参数
        }

        // 公共属性的get/set访问器
        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public double Gamma
        {
            get => (double)GetValue(GammaProperty);
            set => SetValue(GammaProperty, value);
        }

        public double Contrast
        {
            get => (double)GetValue(ContrastProperty);
            set => SetValue(ContrastProperty, value);
        }

        public double Saturation
        {
            get => (double)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, value);
        }
        
        // 4. 当任何一个参数（Gamma, Contrast, Saturation）改变时，此回调函数被调用
        private static void OnParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 将自身转换为Effect对象，并调用更新方法
            (d as InvertEffect)?.UpdateShaderParameters();
        }

        // 5. 核心方法：读取所有参数值，打包成Point4D，并更新到ShaderParametersProperty
        private void UpdateShaderParameters()
        {
            // Point4D的X, Y, Z, W 完美对应 HLSL中float4的x, y, z, w
            SetValue(ShaderParametersProperty, new Point4D(this.Gamma, this.Contrast, this.Saturation, 0.0));
        }
}
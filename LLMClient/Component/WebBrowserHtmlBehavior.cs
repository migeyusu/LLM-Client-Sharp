// Behaviors/WebBrowserHtmlBehavior.cs

using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component
{
    // 泛型参数指定了此 Behavior 可以附加到的控件类型
    public class WebBrowserHtmlBehavior : Behavior<ChromiumWebBrowser>
    {
        // 1. 创建一个可供绑定的依赖属性
        public static readonly DependencyProperty HtmlContentProperty =
            DependencyProperty.Register(
                nameof(HtmlContent), 
                typeof(string), 
                typeof(WebBrowserHtmlBehavior),
                new PropertyMetadata(null, OnHtmlContentChanged));

        public string HtmlContent
        {
            get => (string)GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }
        
        // 2. 当 Behavior 附加到控件时调用
        protected override void OnAttached()
        {
            base.OnAttached();
            // 订阅浏览器初始化完成事件
            AssociatedObject.IsBrowserInitializedChanged += OnBrowserInitialized;
        }

        // 3. 当 Behavior 从控件上分离时调用
        protected override void OnDetaching()
        {
            // 取消订阅，防止内存泄漏
            AssociatedObject.IsBrowserInitializedChanged -= OnBrowserInitialized;
            base.OnDetaching();
        }

        // 4. 当绑定的 HtmlContent 属性发生变化时
        private static void OnHtmlContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as WebBrowserHtmlBehavior;
            if (behavior == null) return;
            
            // 尝试加载内容。如果浏览器尚未初始化，此方法内会处理。
            behavior.LoadHtml();
        }

        // 5. 当浏览器初始化状态改变时
        private void OnBrowserInitialized(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 确保是在初始化完成时（变为 true）才加载
            if ((bool)e.NewValue)
            {
                LoadHtml();
            }
        }
        
        // 6. 核心加载逻辑
        private void LoadHtml()
        {
            // 确保三件事：1. Behavior已附加到控件 2. 浏览器已初始化 3. 有HTML内容可加载
            if (AssociatedObject != null && AssociatedObject.IsBrowserInitialized && !string.IsNullOrEmpty(HtmlContent))
            {
                AssociatedObject.WebBrowser.LoadHtml(HtmlContent, "http://custom.domain/");
            }
        }
    }
}
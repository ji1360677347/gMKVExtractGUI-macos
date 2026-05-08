using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using gMKVToolNix.Controls;

namespace gMKVToolNix.Theming
{
    public enum ThemeMode
    {
        Light = 0,
        Dark = 1,
        Macaron = 2
    }

    /// <summary>
    /// 马卡龙糖果配色 + 视觉模拟磨砂毛玻璃效果。
    /// 通过 GDI+ 自绘叠加圆角面板、半透明白覆盖与柔和阴影；
    /// Form 背景使用粉→紫→薄荷三段竖向渐变营造毛玻璃底色。
    /// </summary>
    internal static class MacaronTheme
    {
        // 背景奶油底
        public static readonly Color Cream = Color.FromArgb(253, 246, 240);
        // 渐变起止色（粉、薰衣草、薄荷、奶蓝）
        public static readonly Color GradientTop = Color.FromArgb(255, 226, 234);     // 樱花粉
        public static readonly Color GradientMid = Color.FromArgb(232, 222, 248);     // 薰衣草
        public static readonly Color GradientBottom = Color.FromArgb(212, 238, 226);  // 薄荷绿

        // 主色板（用于按钮/容器/高亮）
        public static readonly Color Pink = Color.FromArgb(255, 209, 220);
        public static readonly Color Mint = Color.FromArgb(196, 232, 213);
        public static readonly Color Lavender = Color.FromArgb(220, 208, 240);
        public static readonly Color Lemon = Color.FromArgb(255, 240, 192);
        public static readonly Color Sky = Color.FromArgb(205, 228, 244);

        // 文字与边框
        public static readonly Color Text = Color.FromArgb(74, 74, 90);
        public static readonly Color TextSoft = Color.FromArgb(120, 120, 138);
        public static readonly Color GlassFill = Color.FromArgb(190, 255, 255, 255);  // 半透明白磨砂面板
        public static readonly Color GlassBorder = Color.FromArgb(140, 255, 255, 255);
        public static readonly Color Shadow = Color.FromArgb(28, 110, 80, 130);
        public static readonly Color DropTarget = Color.FromArgb(160, 255, 174, 200); // 拖拽高亮粉

        // 输入控件背景（半透明白）
        public static readonly Color InputBack = Color.FromArgb(255, 252, 254);
        public static readonly Color InputBorder = Color.FromArgb(220, 200, 218);

        public const int CornerRadius = 10;
        public const int ShadowOffset = 3;

        // 跟踪已挂载 Paint 钩子的控件，避免重复注册
        private static readonly HashSet<Control> _paintedForms = new HashSet<Control>();
        private static readonly HashSet<Control> _paintedGroupBoxes = new HashSet<Control>();
        private static readonly HashSet<Control> _highlightedDropTargets = new HashSet<Control>();

        /// <summary>渲染 Form 的马卡龙竖向渐变背景。</summary>
        public static void PaintFormBackground(object sender, PaintEventArgs e)
        {
            var ctl = sender as Control;
            if (ctl == null) return;
            Rectangle r = ctl.ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            // 上半段：粉→薰衣草
            using (var brush1 = new LinearGradientBrush(
                new Rectangle(0, 0, r.Width, Math.Max(1, r.Height / 2)),
                GradientTop, GradientMid, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush1, 0, 0, r.Width, Math.Max(1, r.Height / 2));
            }
            // 下半段：薰衣草→薄荷
            using (var brush2 = new LinearGradientBrush(
                new Rectangle(0, r.Height / 2, r.Width, Math.Max(1, r.Height - r.Height / 2)),
                GradientMid, GradientBottom, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush2, 0, r.Height / 2, r.Width, r.Height - r.Height / 2);
            }
        }

        /// <summary>给 Form 挂上渐变背景绘制（幂等）。</summary>
        public static void HookFormBackground(Form form)
        {
            if (form == null || _paintedForms.Contains(form)) return;
            form.Paint += PaintFormBackground;
            form.Resize += FormResizeInvalidate;
            form.Disposed += FormDisposed;
            _paintedForms.Add(form);
            form.Invalidate();
        }

        private static void FormResizeInvalidate(object sender, EventArgs e)
        {
            (sender as Control)?.Invalidate();
        }

        private static void FormDisposed(object sender, EventArgs e)
        {
            var c = sender as Control;
            if (c != null) _paintedForms.Remove(c);
        }

        /// <summary>给 Form 卸下渐变绘制（切换回 Light/Dark 时）。</summary>
        public static void UnhookFormBackground(Form form)
        {
            if (form == null || !_paintedForms.Contains(form)) return;
            form.Paint -= PaintFormBackground;
            _paintedForms.Remove(form);
            form.Invalidate();
        }

        /// <summary>GroupBox 自绘：圆角磨砂玻璃面板 + 标题。</summary>
        public static void PaintGlassGroupBox(object sender, PaintEventArgs e)
        {
            var gb = sender as GroupBox;
            if (gb == null) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 标题字体高度，用于让圆角面板从标题中线起向下绘制
            int titleHeight = string.IsNullOrEmpty(gb.Text) ? 0 : gb.Font.Height;
            int top = titleHeight / 2;
            Rectangle panel = new Rectangle(0, top, gb.Width - 1, gb.Height - top - 1);

            // 软阴影（向右下偏移）
            Rectangle shadowRect = new Rectangle(panel.X + ShadowOffset, panel.Y + ShadowOffset,
                panel.Width - ShadowOffset, panel.Height - ShadowOffset);
            using (var shadowPath = RoundedRect(shadowRect, CornerRadius))
            using (var shadowBrush = new SolidBrush(Shadow))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            // 半透明白玻璃面板
            Rectangle glassRect = new Rectangle(panel.X, panel.Y,
                panel.Width - ShadowOffset, panel.Height - ShadowOffset);
            using (var glassPath = RoundedRect(glassRect, CornerRadius))
            using (var fillBrush = new SolidBrush(GlassFill))
            using (var borderPen = new Pen(GlassBorder, 1f))
            {
                g.FillPath(fillBrush, glassPath);
                g.DrawPath(borderPen, glassPath);
            }

            // 标题
            if (!string.IsNullOrEmpty(gb.Text))
            {
                SizeF textSize = g.MeasureString(gb.Text, gb.Font);
                int titleX = 14;
                // 标题底色挖空（让标题看起来悬浮在面板上）
                Rectangle titleBg = new Rectangle(titleX - 4, 0,
                    (int)textSize.Width + 8, (int)textSize.Height + 2);
                using (var titlePath = RoundedRect(titleBg, 6))
                using (var titleBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    g.FillPath(titleBrush, titlePath);
                }
                using (var brush = new SolidBrush(Text))
                {
                    g.DrawString(gb.Text, gb.Font, brush, titleX, 1);
                }
            }
        }

        public static void HookGroupBox(GroupBox gb)
        {
            if (gb == null || _paintedGroupBoxes.Contains(gb)) return;
            gb.Paint += PaintGlassGroupBox;
            gb.Disposed += (s, e) => _paintedGroupBoxes.Remove(gb);
            _paintedGroupBoxes.Add(gb);
            gb.Invalidate();
        }

        public static void UnhookGroupBox(GroupBox gb)
        {
            if (gb == null || !_paintedGroupBoxes.Contains(gb)) return;
            gb.Paint -= PaintGlassGroupBox;
            _paintedGroupBoxes.Remove(gb);
            gb.Invalidate();
        }

        /// <summary>给 Button 应用马卡龙圆角样式（FlatStyle + Region 圆角）。</summary>
        public static void StyleButton(Button btn)
        {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = InputBorder;
            btn.FlatAppearance.MouseOverBackColor = LightenOrDarken(PickAccentForButton(btn), -0.06f);
            btn.FlatAppearance.MouseDownBackColor = LightenOrDarken(PickAccentForButton(btn), -0.12f);
            btn.BackColor = PickAccentForButton(btn);
            btn.ForeColor = Text;
            btn.UseVisualStyleBackColor = false;
            btn.Cursor = Cursors.Hand;
            ApplyRoundRegion(btn, 8);
            btn.Resize -= ButtonResize;
            btn.Resize += ButtonResize;
        }

        private static void ButtonResize(object sender, EventArgs e)
        {
            ApplyRoundRegion(sender as Control, 8);
        }

        private static void ApplyRoundRegion(Control c, int radius)
        {
            if (c == null || c.Width <= 0 || c.Height <= 0) return;
            using (var path = RoundedRect(new Rectangle(0, 0, c.Width, c.Height), radius))
            {
                c.Region = new Region(path);
            }
        }

        /// <summary>根据按钮名/角色挑选不同的马卡龙强调色，让 UI 不单调。</summary>
        private static Color PickAccentForButton(Button btn)
        {
            string n = (btn.Name ?? string.Empty).ToLowerInvariant();
            if (n.Contains("extract") || n.Contains("ok") || n.Contains("save")) return Mint;
            if (n.Contains("abort") || n.Contains("cancel") || n.Contains("delete") || n.Contains("remove")) return Pink;
            if (n.Contains("browse") || n.Contains("select") || n.Contains("auto")) return Lavender;
            if (n.Contains("show") || n.Contains("log") || n.Contains("job")) return Sky;
            return Lemon;
        }

        public static Color LightenOrDarken(Color c, float factor)
        {
            // factor: -1.0 (黑) ~ +1.0 (白)
            if (factor < 0)
            {
                float k = 1f + factor;
                return Color.FromArgb(c.A,
                    (int)Math.Max(0, c.R * k),
                    (int)Math.Max(0, c.G * k),
                    (int)Math.Max(0, c.B * k));
            }
            else
            {
                return Color.FromArgb(c.A,
                    (int)Math.Min(255, c.R + (255 - c.R) * factor),
                    (int)Math.Min(255, c.G + (255 - c.G) * factor),
                    (int)Math.Min(255, c.B + (255 - c.B) * factor));
            }
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ============ 拖入文件视觉反馈 ============

        /// <summary>在指定容器上显示一个柔和粉色发光边框（拖拽进入时调用）。</summary>
        public static void ShowDropHighlight(Control target)
        {
            if (target == null || _highlightedDropTargets.Contains(target)) return;
            target.Paint += DropHighlightPaint;
            _highlightedDropTargets.Add(target);
            target.Invalidate();
        }

        public static void HideDropHighlight(Control target)
        {
            if (target == null || !_highlightedDropTargets.Contains(target)) return;
            target.Paint -= DropHighlightPaint;
            _highlightedDropTargets.Remove(target);
            target.Invalidate();
        }

        private static void DropHighlightPaint(object sender, PaintEventArgs e)
        {
            var c = sender as Control;
            if (c == null) return;
            Rectangle r = new Rectangle(2, 2, c.Width - 5, c.Height - 5);
            using (var path = RoundedRect(r, CornerRadius))
            using (var pen = new Pen(DropTarget, 3f) { DashStyle = DashStyle.Dash })
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

}

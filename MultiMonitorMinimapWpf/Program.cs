using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using SystemEvents;

namespace MultimonitorMinimap
{
    
    class Program
    {
        static Random random = new Random();

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(_Process_DPI_Awareness value);
        enum _Process_DPI_Awareness
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }

        static Ellipse ellipse;
        static Canvas canvas;
        static Window window;
        static void Draw()
        {
            ellipse = new Ellipse
            {
                Width = cursorradius * 2,
                Height = cursorradius * 2,
                Fill = Brushes.OrangeRed
            };
            window = new Window();
            window.Width = 0;
            window.Height = 0;
            window.MouseLeftButtonDown += Window_MouseLeftButtonDown;
            window.MouseLeftButtonUp += Window_MouseLeftButtonUp;
            window.MouseMove += Window_MouseMove;
            canvas = new Canvas();
            window.Content = canvas;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.Deactivated += Window_Deactivated;
            window.LostFocus += Window_LostFocus;
            window.PreviewLostKeyboardFocus += Window_PreviewLostKeyboardFocus;
            window.AllowsTransparency = true;
            window.Background = Brushes.Transparent;
            window.Show();
            
            var app = new Application();
            app.Exit += App_Exit;
            app.Run();            
        }

        private static void App_Exit(object sender, ExitEventArgs e)
        {
            Telemetry.TrackEvent("Application exited");
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOP = new IntPtr(0);
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_NOZORDER = 0x0004;
        const UInt32 SWP_NOREDRAW = 0x0008;
        const UInt32 SWP_NOACTIVATE = 0x0010;

        const UInt32 SWP_FRAMECHANGED = 0x0020; /* The frame changed: send WM_NCCALCSIZE */
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        const UInt32 SWP_HIDEWINDOW = 0x0080;
        const UInt32 SWP_NOCOPYBITS = 0x0100;
        const UInt32 SWP_NOOWNERZORDER = 0x0200; /* Don’t do owner Z ordering */
        const UInt32 SWP_NOSENDCHANGING = 0x0400; /* Don’t send WM_WINDOWPOSCHANGING */

        const UInt32 TOPMOST_FLAGS =
            SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOSIZE | SWP_NOMOVE | SWP_NOREDRAW | SWP_NOSENDCHANGING;


        private static void SetWindowTOPMOST()
        {
            window.Topmost = true;
            var wih = new WindowInteropHelper(window);
            IntPtr hWnd = wih.Handle;
            SetWindowPos(hWnd, HWND_TOPMOST, (int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height, TOPMOST_FLAGS);
        }

        private static void Window_LostFocus(object sender, RoutedEventArgs e)
        {
            SetWindowTOPMOST();
        }

        private static void Window_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            SetWindowTOPMOST();
        }

        private static void Window_Deactivated(object sender, EventArgs e)
        {
            SetWindowTOPMOST();
        }

        static bool mouseLeftButtonPressed = false;

        private static void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (mouseLeftButtonPressed) {
                e.Handled = true;
                var position = System.Windows.Forms.Cursor.Position;
                window.Left = MouseDownOffsetX + position.X;
                window.Top = MouseDownOffsetY + position.Y;
            }
        }

        private static void Window_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            UIElement el = (UIElement)sender;
            el.ReleaseMouseCapture();
            mouseLeftButtonPressed = false;
        }

        static int MouseDownOffsetX;
        static int MouseDownOffsetY;

        private static void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            UIElement el = (UIElement)sender;
            var result = el.CaptureMouse();
            mouseLeftButtonPressed = true;

            var position = System.Windows.Forms.Cursor.Position;
            MouseDownOffsetX = (int)window.Left - position.X;
            MouseDownOffsetY = (int)window.Top - position.Y;
        }

        const int blinkCountdownStartValue = 5;
        static int blinkCountdown;
        static bool blinkActive;
        static int blinkTop;
        static int blinkLeft;
        static int blinkWidth;
        static int blinkHeight;
        
        const int cursorradius = 5;
        const int screenoffset = 5; // equal to or greater than cursor radius
        const int ZoomFactor = 20;
        static void ShowStats(object sender, EventArgs e)
        {            
            var allScreens = System.Windows.Forms.Screen.AllScreens;

            var minX = 0;
            var minY = 0;
            var maxX = 0;
            var maxY = 0;
            foreach (var screen in allScreens)
            {
                if (screen.Bounds.Right > maxX)
                {
                    maxX = screen.Bounds.Right;
                }
                if (screen.Bounds.Bottom > maxY)
                {
                    maxY = screen.Bounds.Bottom;
                }
                if (screen.Bounds.Left < minX)
                {
                    minX = screen.Bounds.Left;
                }
                if (screen.Bounds.Top < minY)
                {
                    minY = screen.Bounds.Top;
                }
            }

            //var rect = new Rectangle
            //{
            //    Width = (maxX - minX) / ZoomFactor,
            //    Height = (maxY - minY) / ZoomFactor,
            //    Fill = new SolidColorBrush(System.Windows.Media.Colors.AliceBlue)
            //};
            //canvas.Children.Add(rect);

            //var canvas = new Canvas();
            canvas.Children.RemoveRange(0, canvas.Children.Count);            
            //window.Content = canvas;   

            var label = new Label
            {
                Content = "Test",
                FontSize = 18
            };
            //canvas.Children.Add(label);

            const int ZoomFactorInside = 21;
            foreach (var screen in allScreens)
            {
                var rect = new Rectangle
                {
                    Width = screen.Bounds.Width / ZoomFactorInside,
                    Height = screen.Bounds.Height / ZoomFactorInside,
                    Fill = Brushes.AliceBlue,
                    Stroke = Brushes.Aquamarine
                };
                Canvas.SetLeft(rect, screenoffset + (-minX + screen.Bounds.Left) / ZoomFactor + (screen.Bounds.Width / ZoomFactor - rect.Width) / 2);
                Canvas.SetTop(rect, screenoffset + (-minY + screen.Bounds.Top) / ZoomFactor + (screen.Bounds.Height / ZoomFactor - rect.Height) / 2);
                canvas.Children.Add(rect);
                //Console.WriteLine(screen.Bounds);                
            }            
            var cursor = System.Windows.Forms.Cursor.Current;
            var position = System.Windows.Forms.Cursor.Position;
            //ellipse = new Ellipse
            //{
            //    Width = cursorradius * 2,
            //    Height = cursorradius * 2,
            //    Fill = Brushes.OrangeRed
            //};
            //ellipse.IsEnabled = true;
            canvas.Children.Add(ellipse);
            Canvas.SetLeft(ellipse, screenoffset - cursorradius + (-minX + position.X) / ZoomFactor);
            Canvas.SetTop(ellipse, screenoffset - cursorradius + (-minY + position.Y) / ZoomFactor);

            HandleBlink(minX, minY);

            window.Width = screenoffset*2 + (-minX + maxX) / ZoomFactor;// + 15;
            window.Height = screenoffset*2 + (-minY + maxY) / ZoomFactor;// + 15;
        }

        private static void HandleBlink(int minX, int minY)
        {
            if (blinkActive)
            {
                var rect = new Rectangle
                {
                    Fill = Brushes.Transparent,
                    Width = blinkWidth+1,
                    Height = blinkHeight+1,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(rect, screenoffset + (-minX + blinkLeft) / ZoomFactor);
                Canvas.SetTop(rect, screenoffset + (-minY + blinkTop) / ZoomFactor);
                canvas.Children.Add(rect);
                blinkCountdown--;
                if (blinkCountdown <= 0)
                {
                    blinkActive = false;
                }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Telemetry.TrackEvent("Application started");
            try
            {
                SetProcessDpiAwareness(_Process_DPI_Awareness.Process_Per_Monitor_DPI_Aware);
            }
            catch (Exception ex)
            {
                Telemetry.TrackException(ex);
            }

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(ShowStats);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dispatcherTimer.Start();

            SystemListener listener = new SystemListener();
            listener.SystemEvent += new EventHandler<SystemListenerEventArgs>(listener_SystemEvent);

            Draw();
        }

        static void listener_SystemEvent(object sender, SystemListenerEventArgs e)
        {
            if (e.SystemEvent != SystemEvents.SystemEvents.ObjectValueChange
                && e.SystemEvent != SystemEvents.SystemEvents.ObjectNameChange
                && e.SystemEvent != SystemEvents.SystemEvents.ObjectLocationChange
                && e.SystemEvent != SystemEvents.SystemEvents.ObjectReorder
                && e.SystemEvent != SystemEvents.SystemEvents.ObjectDestroy
                && (int)e.SystemEvent != 30102)
            {
                if (e.SystemEvent == SystemEvents.SystemEvents.SystemForeground
                    || e.SystemEvent == SystemEvents.SystemEvents.ObjectFocus)
                {
                    var i = 3;
                    blinkHeight = 0;
                    Console.WriteLine(e.SystemEvent);
                    var hwnd = e.WindowHandle;

                    // iteratively look for a suitable window to highlight
                    while (i > 0) {
                        if (hwnd == null)
                        {  
                            // unexpected null reference
                            Console.WriteLine("null");
                        }
                        GetBlinkPropertiesFromHWND(hwnd, true, true);
                        var style = GetWindowLongPtr(hwnd, WindowLongFlags.GWL_STYLE);
                        var exstyle = GetWindowLongPtr(hwnd, WindowLongFlags.GWL_EXSTYLE);
                        // Console.WriteLine($"styles: {style} {exstyle}");
                        if (blinkHeight * blinkWidth < 300)
                        {
                            // the found rectangle is too small. let's iterate towards the parent of it
                            hwnd = GetParent(hwnd);                            
                            i--;
                        } else
                        {
                            // we found a rectangle that is big enough
                            break;
                        }
                    }

                    var wih = new WindowInteropHelper(window);
                    IntPtr hWnd = wih.Handle;
                    //if (hWnd == e.WindowHandle)
                    //{
                    //    // that's me
                    //    return;
                    //}


                }
            }
        }

        static void GetBlinkPropertiesFromHWND(IntPtr hWnd, bool log, bool blink)
        {
            RECT lpRect;
            var result = GetWindowRect(new HandleRef(null, hWnd), out lpRect);
            if (log)
            {
                Console.WriteLine($"{hWnd.ToString("X")} {result} {lpRect.Left} {lpRect.Top} {lpRect.Right} {lpRect.Bottom}");
            }

            if (blink)
            {

                blinkActive = true;
                blinkCountdown = blinkCountdownStartValue;
                blinkLeft = lpRect.Left;
                blinkTop = lpRect.Top;
                blinkWidth = (lpRect.Right - lpRect.Left) / ZoomFactor;
                blinkHeight = (lpRect.Bottom - lpRect.Top) / ZoomFactor;
            }
        }

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        /// <summary>
        /// Retrieves the handle to the ancestor of the specified window. 
        /// </summary>
        /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved. 
        /// If this parameter is the desktop window, the function returns NULL. </param>
        /// <param name="flags">The ancestor to be retrieved.</param>
        /// <returns>The return value is the handle to the ancestor window.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        enum GetAncestorFlags
        {
            /// <summary>
            /// Retrieves the parent window. This does not include the owner, as it does with the GetParent function. 
            /// </summary>
            GetParent = 1,
            /// <summary>
            /// Retrieves the root window by walking the chain of parent windows.
            /// </summary>
            GetRoot = 2,
            /// <summary>
            /// Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent. 
            /// </summary>
            GetRootOwner = 3
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, WindowLongFlags nIndex);

        enum WindowLongFlags : int
        {
            GWL_EXSTYLE = -20,
            GWLP_HINSTANCE = -6,
            GWLP_HWNDPARENT = -8,
            GWL_ID = -12,
            GWL_STYLE = -16,
            GWL_USERDATA = -21,
            GWL_WNDPROC = -4,
            DWLP_USER = 0x8,
            DWLP_MSGRESULT = 0x0,
            DWLP_DLGPROC = 0x4
        }
    }
}

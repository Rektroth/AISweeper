/*
 * AISweeper - (c) Brian S Rexroth Jr
 * (Yes, I am aware this is technically not an AI.)
 * Version 1.0 - 04/03/2018
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AISweeper
{
    /// <summary>
    /// The main entry class of the application.
    /// </summary>
    internal static class Program
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        // i'm honestly surprised this much work goes into making the mouse click
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        private static readonly Point LOCATION = new Point(15, 101);

        private const int CELL_0 = 0;
        private const int CELL_1 = 1;
        private const int CELL_2 = 2;
        private const int CELL_3 = 3;
        private const int CELL_4 = 4;
        private const int CELL_5 = 5;
        private const int CELL_6 = 6;
        private const int CELL_7 = 7;
        private const int CELL_8 = 8;
        private const int CELL_FLAG = 9;
        private const int CELL_BLANK = 10;
        private const int CELL_BOMB = 11;

        private const int CELL_SIZE = 16;
        private const int CELL_HALF = 8;

        private static readonly Color CELL_0_COLOR = Color.FromArgb(192, 192, 192);
        private static readonly Color CELL_1_COLOR = Color.FromArgb(0, 0, 255);
        private static readonly Color CELL_2_COLOR = Color.FromArgb(0, 128, 0);
        private static readonly Color CELL_3_COLOR = Color.FromArgb(255, 0, 0);
        private static readonly Color CELL_4_COLOR = Color.FromArgb(0, 0, 128);
        private static readonly Color CELL_5_COLOR = Color.FromArgb(128, 0, 0);
        private static readonly Color CELL_6_COLOR = Color.FromArgb(0, 128, 128);
        private static readonly Color CELL_7_COLOR = Color.FromArgb(0, 0, 0);
        private static readonly Color CELL_8_COLOR = Color.FromArgb(128, 128, 128);
        private static readonly Color CELL_FLAG_COLOR = Color.FromArgb(255, 128, 0);
        private static readonly Color CELL_BLANK_COLOR = Color.FromArgb(255, 255, 255);
        private static readonly Color CELL_BOMB_COLOR = Color.FromArgb(192, 128, 64);

        private static readonly Size DEFAULT_SIZE = new Size(30, 16);

        private static Size size = DEFAULT_SIZE;
        private static int[,] grid = new int[DEFAULT_SIZE.Width, DEFAULT_SIZE.Height];
        private static bool[,] cleared = new bool[DEFAULT_SIZE.Width, DEFAULT_SIZE.Height];

        private static int numCleared = 0;
        private static int clickCount = 0;
        private static bool rightClick = true;
        private static bool failed = false;

        /// <summary>
        /// The main entry point of the application.
        /// </summary>
        /// <param name="args">User arguments. [width] [height] [flagging]</param>
        public static void Main(string[] args)
        {
            IntPtr hWnd = FindWindow(null, "Minesweeper X");

            // the minesweeper window needs moved to the top left corner
            // otherwise, we don't know where to look for it
            if (hWnd != IntPtr.Zero)
            {
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (args[i].ToLower())
                {
                    case "-w":
                        if (int.TryParse(args[i + 1], out int width))
                        {
                            size.Width = width;
                        }
                        break;
                    case "-h":
                        if (int.TryParse(args[i + 1], out int height))
                        {
                            size.Height = height;
                        }
                        break;
                    case "-f":
                        if (bool.TryParse(args[i + 1], out bool flag))
                        {
                            rightClick = flag;
                        }
                        break;
                }
            }

            grid = new int[size.Width, size.Height];
            cleared = new bool[size.Width, size.Height];

            for (int i = 0; i < size.Width; i++)
            {
                for (int j = 0; j < size.Height; j++)
                {
                    grid[i, j] = CELL_BLANK;
                    cleared[i, j] = false;
                }
            }

            int failsafe = 0; // a failsafe - if this number goes above the width * height of the grid, the program terminates

            while (!failed && failsafe < size.Height * size.Width)
            {
                failsafe++; // this failsafe prevents the program from going out of control for too long if the user inputs something wrong or there's a program error
                clickCount = 0;

                // right clicks on cells confirmed to be mines
                for (int i = 0; i < size.Width; i++)
                {
                    for (int j = 0; j < size.Height; j++)
                    {
                        // left clicks on cells confirmed to be mines
                        if (grid[i, j] != CELL_BLANK && !cleared[i, j])
                        {
                            int count = 0;

                            for (int k = -1; k < 2; k++)
                            {
                                for (int l = -1; l < 2; l++)
                                {
                                    if (!(k == 0 && l == 0) && i + k >= 0 && j + l >= 0 && i + k < size.Width && j + l < size.Height)
                                    {
                                        if (grid[i + k, j + l] == CELL_FLAG)
                                        {
                                            count++;
                                        }
                                    }
                                }
                            }

                            if (grid[i, j] == count)
                            {
                                for (int k = -1; k < 2; k++)
                                {
                                    for (int l = -1; l < 2; l++)
                                    {
                                        if (i + k >= 0 && j + l >= 0 && i + k < size.Width && j + l < size.Height)
                                        {
                                            if (grid[i + k, j + l] == CELL_BLANK)
                                            {
                                                doLeftMouseClick(i + k, j + l);
                                            }
                                        }
                                    }
                                }

                                clear(i, j);
                            }
                        }

                        // right clicks on cells confirmed to be safe
                        if (!cleared[i, j])
                        {
                            int count = 0;

                            for (int k = -1; k < 2; k++)
                            {
                                for (int l = -1; l < 2; l++)
                                {
                                    if (!(k == 0 && l == 0) && i + k >= 0 && j + l >= 0 && i + k < size.Width && j + l < size.Height)
                                    {
                                        if (grid[i + k, j + l] == CELL_BLANK || grid[i + k, j + l] == CELL_FLAG)
                                        {
                                            count++;
                                        }
                                    }
                                }
                            }

                            if (grid[i, j] == count)
                            {
                                for (int k = -1; k < 2; k++)
                                {
                                    for (int l = -1; l < 2; l++)
                                    {
                                        if (i + k >= 0 && j + l >= 0 && i + k < size.Width && j + l < size.Height)
                                        {
                                            if (grid[i + k, j + l] == CELL_BLANK)
                                            {
                                                doRightMouseClick(i + k, j + l);
                                            }
                                        }
                                    }
                                }

                                clear(i, j);
                            }
                        }
                    }
                }

                // checks for success/failure, terminates if necessary
                if (clickCount == 0)
                {
                    if (numCleared != cleared.Length)
                    {
                        // randomly guesses what to do next if no useful logic can be discerned
                        while (true)
                        {
                            Random r = new Random();
                            int rWidth = r.Next(0, size.Width);
                            int rHeight = r.Next(0, size.Height);

                            if (!cleared[rWidth, rHeight])
                            {
                                doLeftMouseClick(rWidth, rHeight);

                                if (grid[rWidth, rHeight] == CELL_BOMB)
                                {
                                    failed = true;
                                    Console.WriteLine("Failed to complete board!");
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        failed = true;
                        Console.WriteLine("Sucessfully completed the board!");
                    }
                }
            }
        }

        private static void badInput()
        {
            Console.WriteLine("Invalid arguments.");
            Console.WriteLine("Proper syntax:");
            Console.WriteLine("    -w [width]       (integer > 0)   The number of cells wide the Minesweeper board is.");
            Console.WriteLine("    -h [height]      (integer > 0)   The number of cells tall the Minesweeper board is.");
            Console.WriteLine("    -f [flagging]    (boolean)       Whether or not you want mines flagged.");
            failed = true;
        }

        private static void moveCursor(int x, int y)
        {
            Point p = new Point(LOCATION.X + (x * CELL_SIZE) + CELL_HALF, LOCATION.Y + (y * CELL_SIZE) + CELL_HALF);
            Cursor.Position = p;
        }

        private static void doLeftMouseClick(int x, int y)
        {
            moveCursor(x, y);
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
            grid[x, y] = getCellType(x, y);
            clickCount++;
        }

        private static void doRightMouseClick(int x, int y)
        {
            if (rightClick)
            {
                moveCursor(x, y);
                uint X = (uint)Cursor.Position.X;
                uint Y = (uint)Cursor.Position.Y;
                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, X, Y, 0, 0);
            }

            grid[x, y] = CELL_FLAG;
            clickCount++;
            clear(x, y);
        }

        private static int getCellType(int x, int y)
        {
            Thread.Sleep(5); // the Minesweeper app needs 5ms to update the GUI before we can observe it
            Point p = new Point(LOCATION.X + (x * CELL_SIZE) + CELL_HALF, LOCATION.Y + (y * CELL_SIZE) + CELL_HALF);
            Color c = getColorAt(p);

            if (c == CELL_0_COLOR)
            {
                // we want to make sure it's 0 and not blank
                p.Y -= CELL_HALF;
                p.X -= CELL_HALF;
                c = getColorAt(p);

                if (c == CELL_BLANK_COLOR)
                {
                    return CELL_BLANK;
                }

                return CELL_0;
            }
            else if (c == CELL_1_COLOR)
            {
                return CELL_1;
            }
            else if (c == CELL_2_COLOR)
            {
                return CELL_2;
            }
            else if (c == CELL_3_COLOR)
            {
                return CELL_3;
            }
            else if (c == CELL_4_COLOR)
            {
                return CELL_4;
            }
            else if (c == CELL_5_COLOR)
            {
                return CELL_5;
            }
            else if (c == CELL_6_COLOR)
            {
                return CELL_6;
            }
            else if (c == CELL_7_COLOR)
            {
                // we want to make sure it's a 7 and not a bomb
                p.Y -= CELL_HALF - 1;
                p.X -= CELL_HALF - 1;
                c = getColorAt(p);

                if (c == CELL_3_COLOR) // or just Color.Red
                {
                    return CELL_BOMB;
                }

                return CELL_7;
            }
            else if (c == CELL_8_COLOR)
            {
                return CELL_8;
            }
            else
            {
                return CELL_BLANK;
            }
        }

        private static Color getColorAt(Point p)
        {
            // apperently, bitmaps have to be disposed?
            // all I know is, if it doesn't get disposed, an out of memory error eventually occurs on larger grids
            using (Bitmap screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics gdest = Graphics.FromImage(screenPixel))
                {
                    using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        IntPtr hSrcDC = gsrc.GetHdc();
                        IntPtr hDC = gdest.GetHdc();
                        int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, p.X, p.Y, (int)CopyPixelOperation.SourceCopy);
                        gdest.ReleaseHdc();
                        gsrc.ReleaseHdc();
                    }
                }

                return screenPixel.GetPixel(0, 0);
            }
        }

        private static void clear(int x, int y)
        {
            cleared[x, y] = true;
            numCleared++;
        }
    }
}

/*
 * NOTE (04/03/2018)
 * ----
 * Ways this can be made more efficient:
 *      - when clicking a cell that is revealed as 0, read the adjacents without clicking on them
 *      - calculating probability when simple true/false logic is no longer useful
 *          * right now, the program simply guesses when it gets stuck
 *          
 * Also, the app is running about 33% slower than it was yesterday , and I don't know why...
 * So, I need to figure out what I managed to f*ck up.
 */
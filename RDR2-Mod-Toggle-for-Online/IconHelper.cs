using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;

namespace RDR2_Mod_Toggle_for_Online
{
    internal class IconHelper
    {
        private const int SHGSI_ICON = 0x000000100;
        private const int SHGSI_SMALLICON = 0x000000001;

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        private static BitmapImage GetFolderIcon()
        {
            SHSTOCKICONINFO sii = new SHSTOCKICONINFO();
            sii.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));

            SHGetStockIconInfo(0x3, SHGSI_ICON | SHGSI_SMALLICON, ref sii); // 0x3 is the SIID for folder

            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                sii.hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }

            return bitmapImage;
        }

        public static BitmapImage GetIcon(string path, bool isDirectory)
        {
            // If folder, use the default folder icon
            if (isDirectory)
            {
                return GetFolderIcon();
            }

            if (!System.IO.File.Exists(path))
            {
                // Handle the case where the file does not exist
                return null;
            }

            var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var bitmapImage = new BitmapImage();
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }
    }
}

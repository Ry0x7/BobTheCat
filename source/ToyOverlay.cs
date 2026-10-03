using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BobDesktop
{
    // A separate tiny, click-through surface: toys can roam without a screen-sized overlay.
    internal sealed class ToyOverlay : Form
    {
        private IntPtr dc, original;
        private readonly Dictionary<string,IntPtr> frames=new Dictionary<string,IntPtr>();
        private bool disposed;
        public ToyOverlay() { FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.None;Text="Cat toy"; }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams;p.ExStyle|=Native.ExLayered|Native.ExToolWindow|Native.ExNoActivate|Native.ExTransparent;return p; } }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x21) { m.Result=new IntPtr(3);return; }
            if(m.Msg==0x84) { m.Result=new IntPtr(-1);return; }
            base.WndProc(ref m);
        }
        public void DrawToy(ToyKind kind,int frame,double x,double y,double scale)
        {
            if(kind==ToyKind.None) { if(Visible)Hide();return; }
            int size=Math.Max(24,(int)(48*scale));
            string key=kind+":"+frame+":"+size;
            IntPtr bitmap;
            if(!frames.TryGetValue(key,out bitmap))
            {
                using(Bitmap source=ExtraArt.Toy(kind,frame))
                using(Bitmap image=new Bitmap(size,size,PixelFormat.Format32bppPArgb))
                using(Graphics g=Graphics.FromImage(image))
                {
                    g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(source,new Rectangle(0,0,size,size));bitmap=image.GetHbitmap(Color.FromArgb(0));frames.Add(key,bitmap);
                }
            }
            if(dc==IntPtr.Zero)dc=Native.CreateCompatibleDC(IntPtr.Zero);
            IntPtr previous=Native.SelectObject(dc,bitmap);if(original==IntPtr.Zero)original=previous;
            if(!Visible)Show();
            Native.POINT position=new Native.POINT((int)x-size/2,(int)y-size/2),origin=new Native.POINT(0,0);
            Native.SIZE dimensions=new Native.SIZE(size,size);Native.BLEND blend=new Native.BLEND { Op=0,Flags=0,Alpha=255,Format=1 };
            if(!Native.UpdateLayeredWindow(Handle,IntPtr.Zero,ref position,ref dimensions,dc,ref origin,0,ref blend,2)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            Native.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x0010|0x0001|0x0002);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing && !disposed)
            {
                disposed=true;if(dc!=IntPtr.Zero && original!=IntPtr.Zero)Native.SelectObject(dc,original);
                foreach(IntPtr bitmap in frames.Values)Native.DeleteObject(bitmap);frames.Clear();if(dc!=IntPtr.Zero)Native.DeleteDC(dc);
            }
            base.Dispose(disposing);
        }
    }
}

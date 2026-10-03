using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace BobDesktop
{
    // The approved image-generated artwork is embedded unchanged. No redraw or low-res intermediary.
    internal static class ExtraArt
    {
        private static readonly Lazy<Bitmap> actions=new Lazy<Bitmap>(delegate { return Load("Bob.action-sprites.png"); });
        private static readonly Lazy<Bitmap> yarn=new Lazy<Bitmap>(delegate { return Load("Bob.yarn-sprites.png"); });
        private static Bitmap Load(string name)
        {
            using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if(input==null)throw new InvalidDataException("Missing generated artwork: "+name);
                using(Bitmap image=new Bitmap(input))return image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppArgb);
            }
        }
        internal static Bitmap Make(Bitmap original,int row,int frame)
        {
            if(row<20 || row>29 || frame<0 || frame>=6)throw new ArgumentOutOfRangeException("Action frame");
            return actions.Value.Clone(new Rectangle(frame*192,(row-20)*208,192,208),PixelFormat.Format32bppArgb);
        }
        internal static Bitmap Toy(ToyKind kind,int frame)
        {
            if(kind==ToyKind.Yarn)return yarn.Value.Clone(new Rectangle((frame%6)*48,0,48,48),PixelFormat.Format32bppArgb);
            // The laser is a screen-coordinate target, rendered as a precise small point.
            Bitmap image=new Bitmap(48,48,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(image))
            using(Brush glow=new SolidBrush(Color.FromArgb(70,255,60,60)))
            using(Brush red=new SolidBrush(Color.FromArgb(239,60,65)))
            using(Brush center=new SolidBrush(Color.FromArgb(255,235,214)))
            {
                g.FillRectangle(glow,20,18,8,12);g.FillRectangle(glow,18,20,12,8);
                g.FillRectangle(red,22,21,4,6);g.FillRectangle(red,21,22,6,4);g.FillRectangle(center,23,23,2,2);
            }
            return image;
        }
    }
}


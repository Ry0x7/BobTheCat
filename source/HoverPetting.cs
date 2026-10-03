using System;
using System.Drawing;

namespace BobDesktop
{
    internal enum PetRegion { None, Head, Belly }
    // Requires deliberate vertical strokes/reversals over opaque fur; no mouse hooks or clicks.
    internal sealed class HoverPetting
    {
        private PetRegion region;
        private double lastY, started, cooldownUntil, travel;
        private int direction, reversals;
        internal void Reset() { region=PetRegion.None;direction=reversals=0;travel=0; }
        internal PetRegion Update(Point cursor,Rectangle bounds,double scale,bool opaque,double now)
        {
            double x=(cursor.X-bounds.Left)/(double)bounds.Width,y=(cursor.Y-bounds.Top)/(double)bounds.Height;
            PetRegion current=opaque && x>=.20 && x<=.78 && y>=.25 && y<.64?PetRegion.Head:opaque && x>=.30 && x<=.70 && y>=.64 && y<=.95?PetRegion.Belly:PetRegion.None;
            if(current==PetRegion.None || now<cooldownUntil) { Reset();return PetRegion.None; }
            double localY=cursor.Y-bounds.Top;
            if(current!=region || now-started>1.8) { Reset();region=current;started=now;lastY=localY;return PetRegion.None; }
            double delta=localY-lastY;
            if(Math.Abs(delta)<3*scale)return PetRegion.None;
            if(Math.Abs(delta)>55*scale) { Reset();return PetRegion.None; }
            lastY=localY;travel+=Math.Abs(delta);
            int next=delta>0?1:-1;if(direction!=0 && next!=direction)reversals++;direction=next;
            if(reversals>=2 && travel>=24*scale) { PetRegion result=region;Reset();cooldownUntil=now+3.5;return result; }
            return PetRegion.None;
        }
    }
}

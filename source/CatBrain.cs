using System;
using System.Collections.Generic;
using System.Drawing;

namespace BobDesktop
{
    public enum CatAction { None, Loaf, Stretch, Scratch, Groom, Roll, Zoomies, Yarn, Laser, Cling, Fall, Hop, HeadScratch, BellyRub }
    public enum ToyKind { None, Yarn, Laser }

    // A deterministic, UI-independent cat simulation. Desktop geometry is its only window input.
    public class CatBrain
    {
        public double X, Y;
        public bool Paused, ForcedSleep, Moving;
        public int State;
        public double HoldUntil;
        public Behavior Mode;
        public bool WindowInteractions = true, ToysEnabled = true, EnergeticPlay = true, LaserFollowsCursor;
        public bool CursorCuriosity = true;
        public bool Curious { get; private set; }
        public List<WindowLandmark> Windows = new List<WindowLandmark>();
        public bool Perched { get { return phase == 6 && Action == CatAction.None; } }
        public int WindowVisits, YarnBats;
        public CatAction Action { get; private set; }
        public ToyKind Toy { get; private set; }
        public double ToyX, ToyY;
        public bool ToyVisible { get { return Toy != ToyKind.None; } }
        public string ActivityName { get { return Action != CatAction.None ? Action.ToString() : Perched ? "Window perch" : State == -1 ? "Nap" : Moving ? "Exploring" : "Relaxing"; } }
        private readonly Random random;
        private PointF target;
        private IntPtr visitingWindow;
        private int phase, watchDirection;
        private double activityUntil, actionStart, actionUntil, nextTarget, nextBat, toyVx, toyVy, fallVelocity, hopY;
        private bool needsRest, travellingToCling;
        private double curiosityUntil, curiosityCooldown;
        public CatBrain() : this(new Random()) { }
        internal CatBrain(Random source) { random = source; }
        public void Wake() { ForcedSleep = false; State = 0; CancelAction(); Curious=false; needsRest = true; }
        public void CancelAction() { Action = CatAction.None; Toy = ToyKind.None; travellingToCling = false; Curious=false; }
        public void RemoveToy() { if (ToyVisible) { CancelAction(); needsRest = true; } }
        private void Rest(double now) { phase = 0; activityUntil = now + 5 + random.NextDouble() * 12; }
        private PointF RandomPosition(Rectangle area, Size pet)
        {
            return new PointF((float)(area.Left + random.NextDouble() * Math.Max(0, area.Width-pet.Width)), (float)(area.Top + random.NextDouble() * Math.Max(0,area.Height-pet.Height)));
        }
        private WindowLandmark LiveWindow()
        {
            return WindowInteractions ? Windows.Find(delegate(WindowLandmark w) { return w.Handle == visitingWindow; }) : null;
        }
        public bool StartAction(CatAction action, double now, Rectangle area, Size pet)
        {
            List<WindowLandmark> grips=Windows.FindAll(delegate(WindowLandmark w) { return w.Bounds.Top>=area.Top+pet.Height*82.0/208; });
            if (action == CatAction.Cling && (!WindowInteractions || grips.Count == 0)) return false;
            if ((action == CatAction.Yarn || action == CatAction.Laser) && !ToysEnabled) return false;
            CancelAction(); ForcedSleep = false; Paused = false; needsRest = false; HoldUntil = 0;
            Action = action; actionStart = now; nextTarget = now; nextBat = now; phase = 0;
            double duration = action == CatAction.Loaf ? 20 : action == CatAction.Groom || action == CatAction.BellyRub ? 6 : action == CatAction.HeadScratch ? 3 : action == CatAction.Zoomies ? 12 : action == CatAction.Yarn ? 24 : action == CatAction.Laser ? 20 : action == CatAction.Roll ? 3 : action == CatAction.Hop ? .9 : 4;
            actionUntil = now + duration;
            if (action == CatAction.Hop) hopY = Y;
            if (action == CatAction.Fall) { fallVelocity = 0; actionUntil = double.MaxValue; }
            if (action == CatAction.Cling)
            {
                visitingWindow = grips[random.Next(grips.Count)].Handle;
                travellingToCling = true; actionUntil = now + 25;
            }
            if (action == CatAction.Yarn || action == CatAction.Laser)
            {
                Toy = action == CatAction.Yarn ? ToyKind.Yarn : ToyKind.Laser;
                double padding=pet.Width/8.0+2;
                ToyX = Clamp(X + pet.Width + 65, area.Left + padding, area.Right-padding);
                ToyY = Clamp(Y + pet.Height - 12, area.Top + padding, area.Bottom-padding);
                toyVx = toyVy = 0;
            }
            return true;
        }
        private void PlanActivity(double now, Rectangle area, Size pet)
        {
            if (phase != 0) { Rest(now); return; }
            int choice = random.Next(100);
            if (choice < 15 && WindowInteractions && Windows.Count > 0)
            {
                WindowLandmark window = Windows[random.Next(Windows.Count)]; visitingWindow = window.Handle;
                target = window.Position; phase = 5; activityUntil = now + 25;
            }
            else if (choice < 32)
            {
                phase = 1;
                double angle = random.NextDouble()*Math.PI*2, distance=100+random.NextDouble()*260;
                target = new PointF((float)Clamp(X+Math.Cos(angle)*distance,area.Left,area.Right-pet.Width),(float)Clamp(Y+Math.Sin(angle)*distance,area.Top,area.Bottom-pet.Height));
                activityUntil = now+12;
            }
            else if (choice < 40) Rest(now);
            else if (choice < 48) { phase = 2; activityUntil = now+20+random.NextDouble()*25; }
            else if (choice < 53) { phase = 3; activityUntil = now+.84; }
            else if (choice < 59) { phase = 4; watchDirection=random.Next(16); activityUntil=now+4; }
            else
            {
                CatAction[] calm = { CatAction.Loaf, CatAction.Stretch, CatAction.Scratch, CatAction.Groom, CatAction.Roll };
                CatAction[] playful = { CatAction.Loaf, CatAction.Stretch, CatAction.Scratch, CatAction.Groom, CatAction.Roll, CatAction.Zoomies, CatAction.Yarn, CatAction.Laser, CatAction.Cling, CatAction.Hop };
                CatAction[] options = EnergeticPlay ? playful : calm;
                if (!StartAction(options[random.Next(options.Length)],now,area,pet)) Rest(now);
            }
        }
        private bool MoveTo(PointF destination, double speed, double dt, Rectangle area, Size pet)
        {
            double dx=Clamp(destination.X,area.Left,area.Right-pet.Width)-X, dy=Clamp(destination.Y,area.Top,area.Bottom-pet.Height)-Y;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            if (distance <= 4) return true;
            double step=Math.Min(distance,speed*dt); X+=dx/distance*step; Y+=dy/distance*step;
            Moving=true; State=dx>=0?1:2; return false;
        }
        private void TickAction(double dt, double now, Point cursor, Rectangle area, Size pet)
        {
            if ((Action == CatAction.Yarn || Action == CatAction.Laser) && !ToysEnabled) { CancelAction(); Rest(now); return; }
            if (Action == CatAction.Cling)
            {
                WindowLandmark live=LiveWindow();
                if (live==null || live.Bounds.Top<area.Top+pet.Height*82.0/208) { StartAction(CatAction.Fall,now,area,pet); return; }
                PointF grip=new PointF((float)Clamp(live.Bounds.Right-pet.Width-30,area.Left,area.Right-pet.Width),(float)Clamp(live.Bounds.Top-pet.Height*82.0/208,area.Top,area.Bottom-pet.Height));
                if (travellingToCling)
                {
                    if (MoveTo(grip,240,dt,area,pet)) { travellingToCling=false; actionStart=now; actionUntil=now+3.2; WindowVisits++; }
                    else if (now>=actionUntil) { CancelAction(); Rest(now); }
                    return;
                }
                X=grip.X; Y=grip.Y; State=25;
                if (now>=actionUntil) StartAction(CatAction.Fall,now,area,pet);
                return;
            }
            if (Action == CatAction.Fall)
            {
                fallVelocity=Math.Min(700,fallVelocity+1000*dt); Y+=fallVelocity*dt; State=26; Moving=true;
                if (Y>=area.Bottom-pet.Height) { Y=area.Bottom-pet.Height; Moving=false; StartAction(CatAction.Stretch,now,area,pet); State=21; }
                return;
            }
            if (now>=actionUntil)
            {
                if (Action==CatAction.Hop) Y=hopY;
                CancelAction(); Rest(now); State=0; return;
            }
            if (Action == CatAction.Zoomies)
            {
                if (now>=nextTarget) { target=RandomPosition(area,pet); nextTarget=now+1.8+random.NextDouble()*1.2; }
                if (MoveTo(target,340,dt,area,pet)) nextTarget=now;
                return;
            }
            if (Action == CatAction.Yarn || Action == CatAction.Laser)
            {
                double padding=pet.Width/8.0+2;
                if (Action == CatAction.Laser)
                {
                    if (LaserFollowsCursor) { ToyX=Clamp(cursor.X,area.Left+padding,area.Right-padding); ToyY=Clamp(cursor.Y,area.Top+padding,area.Bottom-padding); }
                    else
                    {
                        if (now>=nextTarget) { PointF p=RandomPosition(area,pet); target=new PointF(p.X+pet.Width/2,p.Y+pet.Height*.8f); nextTarget=now+1.5; }
                        double dx=target.X-ToyX,dy=target.Y-ToyY,d=Math.Sqrt(dx*dx+dy*dy),step=Math.Min(d,210*dt);
                        if(d>.01) { ToyX+=dx/d*step; ToyY+=dy/d*step; }
                    }
                }
                else
                {
                    ToyX+=toyVx*dt; ToyY+=toyVy*dt;
                    double friction=Math.Exp(-2.4*dt); toyVx*=friction; toyVy*=friction;
                    if(ToyX<area.Left+padding || ToyX>area.Right-padding) toyVx=-toyVx*.75;
                    if(ToyY<area.Top+padding || ToyY>area.Bottom-padding) toyVy=-toyVy*.75;
                    ToyX=Clamp(ToyX,area.Left+padding,area.Right-padding); ToyY=Clamp(ToyY,area.Top+padding,area.Bottom-padding);
                }
                PointF chase=new PointF((float)(ToyX-pet.Width*.74),(float)(ToyY-pet.Height*.88));
                if(MoveTo(chase,Action==CatAction.Yarn?175:270,dt,area,pet))
                {
                    State=27;
                    if(Action==CatAction.Yarn && now>=nextBat)
                    {
                        double angle=random.NextDouble()*Math.PI*2; toyVx=Math.Cos(angle)*230; toyVy=Math.Sin(angle)*160; nextBat=now+1.0; YarnBats++;
                    }
                }
                return;
            }
            if (Action == CatAction.Hop) { Y=Clamp(hopY-Math.Sin((now-actionStart)/.9*Math.PI)*65,area.Top,area.Bottom-pet.Height); State=27; return; }
            State=Action==CatAction.Loaf?20:Action==CatAction.Stretch?21:Action==CatAction.Scratch?22:Action==CatAction.Groom?23:Action==CatAction.BellyRub?28:Action==CatAction.HeadScratch?29:24;
        }
        public void Tick(double dt, double now, Point cursor, Rectangle area, Size pet, double inputIdle, double cursorIdle, int sleepAfter)
        {
            Moving=false;
            if(Paused) return;
            X=Clamp(X,area.Left,area.Right-pet.Width); Y=Clamp(Y,area.Top,area.Bottom-pet.Height);
            // Finish a fall before sleeping so Bob never remains suspended in mid-air.
            if(ForcedSleep || (inputIdle>=sleepAfter && Action!=CatAction.Fall)) { CancelAction(); needsRest=true; State=-1; return; }
            if(Action!=CatAction.None) { TickAction(dt,now,cursor,area,pet); X=Clamp(X,area.Left,area.Right-pet.Width);Y=Clamp(Y,area.Top,area.Bottom-pet.Height);return; }
            PointF center=new PointF((float)(X+pet.Width/2.0),(float)(Y+pet.Height*.67));
            double dx=cursor.X-center.X,dy=cursor.Y-center.Y,distance=Math.Sqrt(dx*dx+dy*dy);
            if(Mode==Behavior.Wander && CursorCuriosity && now>=HoldUntil && cursorIdle<2 && distance<260 && distance>55 && now>=curiosityCooldown && phase<2)
            { Curious=true; curiosityUntil=now+3; curiosityCooldown=now+18; }
            if(Curious)
            {
                if(!CursorCuriosity || Mode!=Behavior.Wander || now>=curiosityUntil || distance>330 || cursorIdle>3) { Curious=false;Rest(now); }
                else
                {
                    double standOff=Math.Max(65,pet.Width*.4);
                    if(distance>standOff+12) MoveTo(new PointF((float)(cursor.X-dx/distance*standOff-pet.Width/2),(float)(cursor.Y-dy/distance*standOff-pet.Height*.67)),115,dt,area,pet);
                    else { double angle=Math.Atan2(dx,-dy)*180/Math.PI;if(angle<0)angle+=360;State=100+(int)Math.Round(angle/22.5)%16; }
                    return;
                }
            }
            if(Mode==Behavior.Follow && now>=HoldUntil && distance>Math.Max(105,pet.Width*.65)+20 && cursorIdle<7)
            {
                double standOff=Math.Max(105,pet.Width*.65);
                MoveTo(new PointF((float)(cursor.X-dx/distance*standOff-pet.Width/2),(float)(cursor.Y-dy/distance*standOff-pet.Height*.67)),210,dt,area,pet);
            }
            else if(Mode==Behavior.Wander && now>=HoldUntil)
            {
                if(needsRest) { Rest(now); needsRest=false; }
                if(phase==5 || phase==6) { WindowLandmark live=LiveWindow(); if(live==null) Rest(now); else target=live.Position; }
                if(now>=activityUntil) PlanActivity(now,area,pet);
                if(Action!=CatAction.None) { TickAction(dt,now,cursor,area,pet);return; }
                if(phase==2) { State=-1;return; }
                if(phase==3) { State=4;return; }
                if(phase==4) { State=100+watchDirection;return; }
                if(phase==1 || phase==5 || phase==6)
                {
                    if(MoveTo(target,95,dt,area,pet))
                    {
                        if(phase==5) { phase=6;activityUntil=now+12+random.NextDouble()*16;WindowVisits++; }
                        else if(phase!=6) Rest(now);
                    }
                }
            }
            if(!Moving)
            {
                if(Perched) State=((int)now/4)%2==0?0:108;
                else if(Mode!=Behavior.Wander && cursorIdle<3 && distance>22)
                {
                    double angle=Math.Atan2(dx,-dy)*180/Math.PI; if(angle<0) angle+=360;
                    State=100+((int)Math.Round(angle/22.5)%16);
                }
                else State=0;
            }
        }
        public static double Clamp(double value,double lower,double upper) { return Math.Max(lower,Math.Min(Math.Max(lower,upper),value)); }
    }
}

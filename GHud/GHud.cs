﻿//#define TEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

using UnityEngine;

// When complied in debug mode, we override a few things so we can run without KSP
#if TEST
namespace UnityEngine
{
    public class Debug
    {
        public static void Log(string s) { Console.WriteLine(s); }
        public static void LogWarning(string s) { Console.WriteLine(s); }
        public static void LogError(string s) { Console.WriteLine(s); }
    }
}

public class MonoBehaviour
{

}
#endif

namespace GHud
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class GHud : MonoBehaviour
    {
        List<Device> devices = new List<Device>();  
        
        //private static string appPath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/";

		public static GHud GHudmain;

        public bool test_mode = false;

        private int config = 0;

        private bool lcd_initialized = false;

        private bool lcd_idle = false;

        private String[] font_names = new String[]  {"Inconsolata Medium", "Arial", "Arial Narrow", "Consolas", "Terminal", "Segoe UI Light", "Segoe UI"};

        public void OnDestroy()
        {
            foreach (Device dev in devices)
            {
                dev.Dispose();
            }
            if(lcd_initialized)
                DMcLgLCD.LcdDeInit();
        }
        
        public void cfgCallback(int cfgConnection)
        {
            config = cfgConnection;
        }
     
        public void TestMode()
        {
            test_mode = true;
        }

        protected void ButtonUp(Device dev)
        {
        }

        protected void ButtonDown(Device dev)
        {
        }

        public void ButtonLeft(Device dev)
        {
        }

        public void ButtonRight(Device dev)
        {
        }

        public void ButtonOk(Device dev)
        {
        }

        public void ButtonCancel(Device dev)
        {
        }

        // Cycle through the existing modules
        public void ButtonMenu(Device dev)
        {
            bool activate = false;
            bool activated = false;

            foreach (DisplayModule dmod in dev.modules)
            {
                if (activate)
                {
                    dmod.Activate();
                    activated = true;
                    break;
                }
                if (dmod.active)
                {
                    activate = true;
                    dmod.Deactivate();
                }
            }

            if (!activated)
            {
                dev.modules[0].Activate();
            }
        }

             
        public void OnGUI()
        {
            //StartCoroutine(Snarfer());
            //Snarfer();
        }

        public void Awake()
        {
			if (GHudmain != null) return;
			GHudmain = this;
#if !TEST
			UnityEngine.Object.DontDestroyOnLoad(GHudmain);
#endif

            if (!lcd_initialized)
            {
                DMcLgLCD.LcdInit();
                lcd_initialized = true;
            }

            Device bw_dev = new DeviceBW();
            Device color_dev = new DeviceQVGA();

            if (bw_dev != null && bw_dev.isValid())
            {
                devices.Add(bw_dev);
                OrbitInfo initialbw = new OrbitInfo(bw_dev, "✈", System.Drawing.Color.Black, System.Drawing.Color.Black);
                initialbw.Activate();
                bw_dev.modules.Add(initialbw);

                
                OrbitInfo targetinfo = new OrbitInfo(bw_dev, "+", System.Drawing.Color.Black, System.Drawing.Color.Black);
                targetinfo.is_target_type_module = true;
                bw_dev.modules.Add(targetinfo);

                bw_dev.modules.Add(new OrbitGraph(bw_dev, System.Drawing.Color.Yellow, "✈"));
                OrbitGraph tgt_orbitgraph = new OrbitGraph(bw_dev, System.Drawing.Color.Yellow, "+");
                tgt_orbitgraph.is_target_type_module = true;
                bw_dev.modules.Add(tgt_orbitgraph);
            }

            if (color_dev != null && color_dev.isValid())
            {
                devices.Add(color_dev);
                VesselInfo initialcolor = new VesselInfo(color_dev);
                initialcolor.Activate();
                color_dev.modules.Add(initialcolor);
                //color_dev.modules.Add(new OrbitInfo(color_dev, "✈", System.Drawing.Color.FromArgb(0xee, 0xee, 0x00), System.Drawing.Color.FromArgb(0xaa, 0xaa, 0x44)));
                /*
                OrbitInfo col_targetinfo = new OrbitInfo(color_dev, "⊹", System.Drawing.Color.LightBlue, System.Drawing.Color.MediumPurple);
                col_targetinfo.is_target_type_module = true;
                color_dev.modules.Add(col_targetinfo);
                 */
                color_dev.modules.Add(new OrbitGraph(color_dev, System.Drawing.Color.Yellow, "✈"));
                OrbitGraph tgt_orbitgraph = new OrbitGraph(color_dev, System.Drawing.Color.LightBlue, "+");
                tgt_orbitgraph.is_target_type_module = true;
                color_dev.modules.Add(tgt_orbitgraph);
            }
            
            foreach (Device dev in devices)
            {
                dev.ButtonUP += new Device.ButtonHandler(ButtonUp);
                dev.ButtonDOWN += new Device.ButtonHandler(ButtonDown);
                dev.ButtonLEFT += new Device.ButtonHandler(ButtonLeft);
                dev.ButtonRIGHT += new Device.ButtonHandler(ButtonRight);
                dev.ButtonOK += new Device.ButtonHandler(ButtonOk);
                dev.ButtonCANCEL += new Device.ButtonHandler(ButtonCancel);
                dev.ButtonMENU += new Device.ButtonHandler(ButtonMenu);

                dev.DisplayFrame();
            }
        }

        // this pattern of using a coroutine to periodically execute things that don't actually need to
        // source: the bottom of this page http://docs.unity3d.com/Manual/Coroutines.html
        public void Start()
        {
            StartCoroutine(LcdUpdateLoop());
        }

        IEnumerator LcdUpdateLoop()
        {
            float wait_time = 1.0f;
            for (; ; )
            {
                wait_time = LcdUpdate();
                yield return new WaitForSeconds(wait_time);
            }
        }

        // now returns a float (interpreted as seconds) defining how loang to wait before the next call, see function above
        public float LcdUpdate()
        {
            // this is how long we wait between LCD updates depending on if we're idle or active
            const float wait_idle = 2.0f;
            const float wait_active = 0.2f;

#if !TEST
            // this is needed to avoid exceptions due to FlightGlobals not existing (I think), which otherwise terminates the update loop
            // IsGame seems to be true only when a game is loaded/active (i.e. not in the main menu)
            if (!HighLogic.LoadedSceneIsGame)
                return wait_idle*2;

            Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) {
                if (lcd_idle) return wait_idle;
				foreach (Device dev in devices) {
					dev.ClearLCD("Waiting for Flight...");
					dev.DisplayFrame();
				}
                lcd_idle = true;
                return wait_idle;
            }

            lcd_idle = false;
#endif
            foreach (Device dev in devices)
            {
                dev.ClearLCD("");  
                dev.DoButtons();
                
                foreach (DisplayModule dmod in dev.modules)
                {
#if !TEST
                    // FIXME This needs a rewrite.  All this crap should be done in the display classes.
                    ITargetable target;
                    target = FlightGlobals.fetch.VesselTarget;
                    Orbit orbit = null;
                    String name = "Unknown";
                    if (dmod.is_target_type_module){
                        if(target == null){
                            dmod.ModuleMsg("No Target", new Rectangle(0,0,0,0));
                        }else{
                            orbit = target.GetOrbit();
                            name = target.GetName();
                        }
                    }else{
                        orbit = vessel.orbit;
                        name = vessel.GetName();
                    }
					if (orbit != null)
					{
						dmod.SetOrbit(orbit, name);
						dmod.Render(new Rectangle(0, 0, 0, 0));
					}
#else
                    if (test_mode)
                    {
                        dmod.TestRender(new Rectangle(0, 0, 0, 0));
                    }
#endif
                }
                dev.DisplayFrame();
            }
            return wait_active;
        }

    }




    // This class contains a few utility functions for getting display units.   Borrwed form Mechjeb
    public class Util
    {
        //From http://svn.xMuMech.com/KSP/trunk/xMuMechLib/MuUtils.cs
        public static string xMuMech_ToSI(double d, ref String suffix)
        {
            int digits = 2;
            double exponent = Math.Log10(Math.Abs(d));
            if (Math.Abs(d) >= 1)
            {
                switch ((int)Math.Floor(exponent))
                {
                    case 0:
                    case 1:
                    case 2:
                        suffix = "";
                        return d.ToString("F" + digits);
                    case 3:
                    case 4:
                    case 5:
                        suffix = "k";
                        return (d / 1e3).ToString("F" + digits);
                    case 6:
                    case 7:
                    case 8:
                        suffix = "M";
                        return (d / 1e6).ToString("F" + digits);
                    case 9:
                    case 10:
                    case 11:
                        suffix = "G";
                        return (d / 1e9).ToString("F" + digits);
                    case 12:
                    case 13:
                    case 14:
                        suffix = "T";
                        return (d / 1e12).ToString("F" + digits);
                    case 15:
                    case 16:
                    case 17:
                        suffix = "P";
                        return (d / 1e15).ToString("F" + digits);
                    case 18:
                    case 19:
                    case 20:
                        suffix = "E";
                        return (d / 1e18).ToString("F" + digits);
                    case 21:
                    case 22:
                    case 23:
                        suffix = "Z";
                        return (d / 1e21).ToString("F" + digits);
                    default:
                        suffix = "Y";
                        return (d / 1e24).ToString("F" + digits);
                }
            }
            else if (Math.Abs(d) > 0)
            {
                switch ((int)Math.Floor(exponent))
                {
                    case -1:
                    case -2:
                    case -3:
                        suffix = "m";
                        return (d * 1e3).ToString("F" + digits);
                    case -4:
                    case -5:
                    case -6:
                        suffix = "μ";
                        return (d * 1e6).ToString("F" + digits);
                    case -7:
                    case -8:
                    case -9:
                        suffix = "n";
                        return (d * 1e9).ToString("F" + digits);
                    case -10:
                    case -11:
                    case -12:
                        suffix = "p";
                        return (d * 1e12).ToString("F" + digits);
                    case -13:
                    case -14:
                    case -15:
                        suffix = "f";
                        return (d * 1e15).ToString("F" + digits);
                    case -16:
                    case -17:
                    case -18:
                        suffix = "a";
                        return (d * 1e18).ToString("F" + digits);
                    case -19:
                    case -20:
                    case -21:
                        suffix = "z";
                        return (d * 1e21).ToString("F" + digits);
                    default:
                        suffix = "y";
                        return (d * 1e24).ToString("F" + digits);
                }
            }
            else
            {
                suffix = "";
                return "0";
            }
        }



        public static string ConvertInterval(double seconds, bool do_years)
        {
            //string format_1 = "{0:D1}y {1:D1}d {2:D2}h {3:D2}m {4:D2}.{5:D1}s";
            //string format_2 = "{0:D1}d {1:D2}h {2:D2}m {3:D2}.{4:D1}s";
            //string format_3 = "{0:D2}h {1:D2}m {2:D2}.{3:D1}s";

            string format_1 = "{0:D1}y {1:D1}d {2:D2}:{3:D2}:{4:D2}";
            string format_2 = "{0:D1}d {1:D2}:{2:D2}:{3:D2}";
            string format_3 = "{0:D2}:{1:D2}:{2:D2}";

            TimeSpan interval = TimeSpan.FromSeconds(seconds);
            int years = interval.Days / 365;

            string output;
            if (years > 0 && do_years)
            {
                output = string.Format(format_1,
                    years,
                    interval.Days - (years * 365), //  subtract years * 365 for accurate day count
                    interval.Hours,
                    interval.Minutes,
                    interval.Seconds);
                //interval.Milliseconds.ToString().Substring(0, 1));
            }
            else if (interval.Days > 0)
            {
                output = string.Format(format_2,
                    interval.Days,
                    interval.Hours,
                    interval.Minutes,
                    interval.Seconds);
                //interval.Milliseconds.ToString().Substring(0, 1));
            }
            else
            {
                output = string.Format(format_3,
                    interval.Hours,
                    interval.Minutes,
                    interval.Seconds);
                //interval.Milliseconds.ToString().Substring(0, 1));
            }
            return output;
        }
    }

    
}



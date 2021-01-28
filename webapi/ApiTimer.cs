using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webapi.Controllers;


namespace webapi
{
    public class ApiTimer
    {
        public static System.Timers.Timer apiTimer;

        public static void SetTimer(int time, System.Timers.ElapsedEventHandler onElapse)
        {
            // Create a timer with a nine second interval.
            apiTimer = new System.Timers.Timer(time);

            // Hook up the Elapsed event for the timer. 
            apiTimer.Elapsed += onElapse;
            apiTimer.AutoReset = false;
            apiTimer.Enabled = true;
        }
        
        public static void RemoveApiTimer(System.Timers.ElapsedEventHandler onElapse)
        {
            apiTimer.Elapsed -= onElapse;
        }

    }
}


/*
 // 🚧🚧🚧🚧 Set timer
            System.Timers.Timer test = new System.Timers.Timer(2000);
            test.Enabled = true;
            test.AutoReset = false;
            test.Elapsed += Test_Elapsed;
            // [REMOVE] => test.Elapsed -= TimerOnElapsed;
 */
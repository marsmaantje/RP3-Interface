using System;


namespace RP3_Interface
{
    //Track and update State variables
    public class State
    {
        //angular values (instant, end, start)-> in Rower
        protected float theta_start, theta_end;
        protected float w_end, w_start;
        public float linearDist, linearVel;
       

        public State()
        {
            this.reset();
        }

        public void setStart(float t, float w) 
        {
            theta_start = t;
            w_start = w;
        }

        public void setEnd(float t, float w)
        {
            theta_end = t;
            w_start = w;
        }

        public void UpdateValues()
        {
            //tbd
        }
       
        public void linearCalc(float k, float theta, float w) //or do this at the rower self?
        {
            linearDist = k * theta;
            linearVel = k * w;
        }

       public void reset() //reset all variables
        {
            theta_start = theta_end = 0f;
            w_start = w_end = 0f;
            linearDist = linearVel = 0f;
        }
    }
    
    public class Drive: State
    {
       //for later; add energy related functions here
    }

    public class Recovery: State
    {        
        public float calcDF(float I, float recTime)
        {
            //Inertia * angular accleration / recovery time
            return (I * ((1 / this.w_start) - (1 / this.w_end)) / recTime) * 1000000;
        }
    }
}
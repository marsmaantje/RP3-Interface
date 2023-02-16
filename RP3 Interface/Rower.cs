using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Diagnostics;



/*
 * Distance over time is the performance output of the C2
 * 
 * 
 * Remarks:
 * Neos input format : {C/F,time,} 
 * to add?: strokelength current?
 * 
 * assumptions:
 * Recovery->Assume linear deceleration
 * Drag force proportional to (rotational) velocity^2
 * 
 * to fix:
 * calculate first angular displacement into virtual boat speed, then later on make it into speed
 * or calculate from here the angular velocity?
 * Distinguish between instant and linear values..., create instantVelocity etc.
 * 
 * 
 * linear distance; the estimated distance boat is expected to travel. Do we need the total angular displacement from start?
 * Linear velocity; the speed at which the boat is expected to travel
 * 
 * instantAngularVelocity; instant calculation of angular velocity, prone to errors.
 * To avoid data spikes, a running average of currentDt is run
 * OpenRowing uses average of 3-> more accurate/responsive; 6-> more smooth for recreational rowing.
 * 
 * 
 * for later:
 * RP3 flywheel weight: 21.5lkg
 * 
 * 
 * 16-02 meeting Laura
 * 
 * Fixed df of 120 (indicates heavy rowing)
 * Moment of cycle indication
 * 
 */


namespace RP3_Interface
{
    public class Rower
    {

        //State tracking
        public enum State { Idle, Drive, Recovery };
        private State currState;
        private Drive drive;
        private Recovery recovery;
        
        //RP3 input
        public float currentDt;
        const int nImpulse = 4;
        int totalImpulse;
        double TotalTime;

        //running average
        public int n_runningAvg = 3;
        private Queue<double> AverageQueue;

        //constants
        const float angularDis = (2 * (float)Math.PI) / nImpulse;
        const float inertia = 0.1001f;
        const float magicFactor = 2.8f;

        //angular values
        public float currTheta, currW;
        public float dragFactor;
        private float conversionFactor;


        public Rower()
        {
            //state idle on start? or use incoming message to do?
            this.currState = State.Drive;
            this.drive = new Drive();
            this.recovery = new Recovery();

            AverageQueue = new Queue<double>(n_runningAvg);

            reset(); //set initial values
        }

        public double RunningAverage(double dt)
        {
           if (AverageQueue.Count == n_runningAvg)
            {
                AverageQueue.Dequeue(); //remove last dt, does not auto do it
            }
            AverageQueue.Enqueue(dt); //add to queue

            return AverageQueue.Sum()/n_runningAvg;
        }


        public void onImpulse(double currentDt)
        {
            //RP3 input
            this.currentDt = (float)RunningAverage(currentDt);
            totalImpulse++;
            TotalTime += currentDt;

            //angular values
            this.currTheta = angularDis * totalImpulse;
            this.currW = angularDis / this.currentDt;

            //convert from rotational to linear values
            if (currState == State.Drive) {
                drive.linearCalc(conversionFactor, currTheta, currW);
                Console.Write(string.Format("LinearDist : {0:0.000#####}", drive.linearDist));
                Console.WriteLine(string.Format(" LinearVel : {0:0.000#####}", drive.linearVel));
                Console.Write(string.Format("w_start : {0:0.000#####}", drive.w_start));
                Console.WriteLine(string.Format(" w_end : {0:0.000#####}", drive.w_end));
            }
            if (currState == State.Recovery) {
                recovery.linearCalc(conversionFactor, currTheta, currW);
                Console.Write(string.Format("LinearDist : {0:0.000#####}", recovery.linearDist));
                Console.WriteLine(string.Format(" LinearVel : {0:0.000#####}", recovery.linearVel));
                Console.Write(string.Format("w_start : {0:0.000#####}", recovery.w_start));
                Console.WriteLine(string.Format(" w_end : {0:0.000#####}", recovery.w_end));
            }

            //debugger
            Console.WriteLine("Current state: "+ currState.ToString());
            Console.WriteLine(string.Format("Total impulses : {0:0.000#####}", this.totalImpulse));
            Console.WriteLine(string.Format("deltaTime : {0:0.000#####}", this.currentDt));
            Console.WriteLine(string.Format("DF : {0:0.000#####}", this.dragFactor));
            Console.WriteLine(string.Format("CF : {0:0.000#####}", this.conversionFactor));
            //Console.Write(string.Format("AngDis : {0:0.000#####}", currTheta)); 
            //Console.WriteLine(string.Format(" angVel : {0:0.000#####}", currW));
            Console.WriteLine("");

            //send back data to Neos
            this.SendDataFormat();
        }

        //triggers when socket receives message
        public void onStateSwitch(string data)
        {

            string d = data;
            Console.WriteLine("Incoming: " + data);
            float[] values = convert(d);


            //end of state is called on Catch, and finish. Need switch
            EndOfState(values, inertia, currTheta, currW);

            if (d.StartsWith("C"))
            {
                this.currState = State.Drive;
            } else if (d.StartsWith("F"))
            {
                this.currState = State.Recovery;
            }
            else if (d.StartsWith("I"))
            {
                this.currState = State.Idle;
            }
        }
        private float[] convert(string data)
        {
            string[] d = data.Split(',');
            string[] values = d.Skip(1).ToArray(); //skip first element, create new array
            var parsedValues = Array.ConvertAll(values, float.Parse);

            return parsedValues;
        }

        private void EndOfState(float[] v, float I, float t, float w)
        {

            float time = v[0];
            //rest to be assigned


            switch (this.currState)
            {
                case State.Drive:

                    this.drive.setEnd(t, w);
                    this.recovery.setStart(t, w);
                    this.drive.linearCalc(conversionFactor, t, w);

                    break;
                case State.Recovery:
                    
                    this.recovery.setEnd(t, w);
                    this.drive.setStart(t, w);

                    
                    //this.dragFactor = this.recovery.calcDF(I, time, this.dragFactor);
                    this.conversionFactor = updateConversionFactor();

                    this.recovery.linearCalc(conversionFactor, t, w);

                    break;
                /*case State.Idle:
                    this.drive.reset();
                    this.recovery.reset();
                    this.reset();
                    break;*/
            }
        }

        private float updateConversionFactor()
        {
            return (float)Math.Pow(((Math.Abs(dragFactor)/1000000) / magicFactor), 1f / 3f);
        }

        public string SendDataFormat()
        {

            if (currState == State.Drive) return $"{currentDt}/{drive.linearDist}/{drive.linearVel}\n";
            else return $"{currentDt}/{recovery.linearDist}/{recovery.linearVel}\n";
        }

        private void reset()
        {
            this.totalImpulse = 0;
            this.TotalTime = 0;
            this.AverageQueue.Clear();
            this.dragFactor = 120; // between 100 and 125, 1e-6 is accounted for
            this.conversionFactor = updateConversionFactor();
        }

    }
  
}

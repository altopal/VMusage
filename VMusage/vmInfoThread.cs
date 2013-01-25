﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using System.Net.Sockets;
using System.Net;

using System.Runtime.InteropServices;

using System.Threading;

namespace VMusage
{
    public class vmInfoThread:IDisposable
    {
        /// <summary>
        /// handle to release capture
        /// </summary>
        WaitHandle eventEnableCapture;
        /// <summary>
        /// handle to release data send
        /// </summary>
        WaitHandle eventEnableSend;
        /// <summary>
        /// how often to build a usage list
        /// </summary>
        public int _iTimeOut = 3000;

        Thread myThread = null;
        bool bStopMainThread = false;

        //for udp send
        Thread myThreadSocket = null;
        bool bStopSocketThread = false;

        Socket sendSocket;

        //Queue<ProcessStatistics.process_statistics> procStatsQueue;
        Queue<byte[]> procStatsQueueBytes;

        object lockQueue = new object();

        public vmInfoThread()
        {
            eventEnableCapture = new AutoResetEvent(true);
            eventEnableSend = new AutoResetEvent(false);

            //procStatsQueue = new Queue<ProcessStatistics.process_statistics>();
            procStatsQueueBytes = new Queue<byte[]>();

            myThreadSocket = new Thread(socketThread);
            myThreadSocket.Start();

            myThread = new Thread(usageThread);
            myThread.Start();
        }
        public void Dispose()
        {
            bStopMainThread = true;
            bStopSocketThread = true;
            if (myThread != null)
            {
                myThread.Abort();
                Thread.Sleep(100);
                myThread = null;
            }
            if (myThreadSocket != null)
            {
                myThreadSocket.Abort();
                Thread.Sleep(100);
                myThreadSocket = null;
            }
        }
        public void sendEndOfTransfer()
        {
            if (sendSocket != null)
                sendSocket.Send(ByteHelper.endOfTransferBytes);
        }
        /// <summary>
        /// send enqueued objects via UDP broadcast
        /// </summary>
        void socketThread()
        {
            System.Diagnostics.Debug.WriteLine("Entering socketThread ...");
            try
            {
                const int ProtocolPort = 3002;
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                sendSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 32768);

                IPAddress sendTo = IPAddress.Broadcast;// IPAddress.Parse("192.168.128.255");  //local broadcast
                EndPoint sendEndPoint = new IPEndPoint(sendTo, ProtocolPort);

                //UdpClient udpC = new UdpClient("255.255.255.255", 1111);
                System.Diagnostics.Debug.WriteLine("Socket ready to send");

                while (!bStopSocketThread)
                {
                    //block until released by capture
                    eventEnableSend.WaitOne();
                    lock (lockQueue)
                    {
                        //if (procStatsQueue.Count > 0)
                        while (procStatsQueueBytes.Count > 0)
                        {
                            byte[] buf = procStatsQueueBytes.Dequeue();
                            if (ByteHelper.isEndOfTransfer(buf))
                                System.Diagnostics.Debug.WriteLine("sending <EOT>");

                            sendSocket.SendTo(buf, buf.Length, SocketFlags.None, sendEndPoint);
                            System.Diagnostics.Debug.WriteLine("Socket send " + buf.Length.ToString() + " bytes");
                            System.Threading.Thread.Sleep(2);
                        }
                    }
                    ((AutoResetEvent)eventEnableCapture).Set();
                }

            }
            catch (ThreadAbortException ex)
            {
                System.Diagnostics.Debug.WriteLine("ThreadAbortException: socketThread(): " + ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception: socketThread(): " + ex.Message);
            }
            System.Diagnostics.Debug.WriteLine("socketThread ENDED");
        }
        /// <summary>
        /// build thread and process list periodically and fire update event and enqueue results for the socket thread
        /// </summary>
        void usageThread()
        {
            try
            {
                int interval = 3000;
                //rebuild a new mem usage info
                VMusage.CeGetProcVMusage vmInfo = new CeGetProcVMusage();

                while (!bStopMainThread)
                {
                    eventEnableCapture.WaitOne();
                    List<VMusage.procVMinfo> myList = vmInfo._procVMinfo; //get a list of processes and the VM usage
                    
                    System.Threading.Thread.Sleep(interval);

                    uint _totalMemUse = 0;
                    foreach(VMusage.procVMinfo pvmi in myList){
                        procStatsQueueBytes.Enqueue(pvmi.toByte());
                        _totalMemUse += pvmi.memusage;
                    }

                    onUpdateHandler(new procVMinfoEventArgs(myList, _totalMemUse));
                    procStatsQueueBytes.Enqueue(ByteHelper.endOfTransferBytes);
                    ((AutoResetEvent)eventEnableSend).Set();
                }//while true
            }
            catch (ThreadAbortException ex)
            {
                System.Diagnostics.Debug.WriteLine("ThreadAbortException: usageThread(): " + ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception: usageThread(): " + ex.Message);
            }
            System.Diagnostics.Debug.WriteLine("Thread ENDED");
        }
        public delegate void updateEventHandler(object sender, procVMinfoEventArgs eventArgs);
        public event updateEventHandler updateEvent;
        void onUpdateHandler(procVMinfoEventArgs procStats)
        {
            //anyone listening?
            if (this.updateEvent == null)
                return;
            this.updateEvent(this, procStats);
        }
    }
}

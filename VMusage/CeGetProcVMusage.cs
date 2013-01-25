﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using System.Runtime.InteropServices;

using System.Process;

namespace VMusage
{
    class CeGetProcVMusage
    {

        private string[] processNames;
        public string[] _processNames
        {
            get {
                getProcVM();
                return processNames; 
            }
        }

        public List<procVMinfo> _procVMinfo{
            get
            {
                procVMinfoList.Clear();
                procVMinfoList = new List<procVMinfo>();
                getProcVM();
                return procVMinfoList;
            }
        }
        List<procVMinfo> procVMinfoList;
        /// <summary>
        /// contructor
        /// </summary>
        public CeGetProcVMusage(){
            processNames = new string[32];
            procVMinfoList = new List<procVMinfo>();
            getProcVM();
        }

        void getProcessNames()
        {
            
	        IntPtr hProcessSnap;
	        IntPtr hProcess;
	        Process.PROCESSENTRY32 pe32=new Process.PROCESSENTRY32();
            pe32.dwSize=(uint)(Marshal.SizeOf(typeof(Process.PROCESSENTRY32)));
	        uint slot;
	        uint STARTBAR=1;
	        uint NUMBARS=32;

	        for(slot=STARTBAR;slot<STARTBAR+NUMBARS;slot++)
	        {
		        processNames[slot-STARTBAR] = String.Format("Slot {0}: empty", slot);
	        }
	        if((1-STARTBAR)>=0)
		        processNames[1-STARTBAR]=String.Format("ROM DLLs");// "Slot 1: ROM DLLs");

	        // Take a snapshot of all processes in the system.
            uint oldPermissions = Process.SetProcPermissions(0xffffffff);
            hProcessSnap = Process.CreateToolhelp32Snapshot(Process.SnapshotFlags.Process | Process.SnapshotFlags.NoHeaps, 0);
            if (hProcessSnap != IntPtr.Zero)
            {
                int iRes = Process.Process32First(hProcessSnap, ref pe32);
                if (iRes != 0)
                {
                    do
                    {
                        hProcess = Process.OpenProcess(Process.ProcessAccessFlags.QueryInformation, false, (int)(pe32.th32ProcessID));
                        if (hProcess != IntPtr.Zero)
                        {
                            slot = pe32.th32MemoryBase / 0x02000000;
                            if (slot - STARTBAR < NUMBARS)
                            {
                                //processNames[slot - STARTBAR] = String.Format("Slot {0}: {1}", slot, pe32.szExeFile);
                                processNames[slot - STARTBAR] = String.Format("{0}", pe32.szExeFile);
                            }

                            Process.CloseHandle(hProcess);
                        }
                    } while (Process.Process32Next(hProcessSnap, ref pe32) != 0);
                }
                else
                    System.Diagnostics.Debug.WriteLine("Process32First failed with " + Marshal.GetLastWin32Error().ToString());

                Process.CloseToolhelp32Snapshot(hProcessSnap);
            }
            Process.SetProcPermissions(oldPermissions);
        }

        StringBuilder getProcVM(){
	        StringBuilder str=new StringBuilder(1024);
	        for (int i=0; i<32; i++){
		        processNames[i]="";
	        }
	        getProcessNames();
            procVMinfoList.Clear();

	        StringBuilder tempStr=new StringBuilder();
	        uint total = 0;
            int idx = 0;
            int STARTBAR = 1;
            uint NUMBARS = 32;
	        //for( int idx = 1; idx < 33; ++idx )
            for (idx = STARTBAR; idx < STARTBAR + NUMBARS; idx++)
	        {
		        Process.PROCVMINFO vmi=new Process.PROCVMINFO();
                int cbSize=Marshal.SizeOf (typeof(Process.PROCVMINFO) );
		        if( Process.CeGetProcVMInfo( idx, cbSize, ref vmi ) !=0 )
		        {
			        //wsprintf(tempStr, L"%d: %d bytes\r\n", idx, vmi.cbRwMemUsed );
			        str.Append( String.Format("%d (%s): %d bytes\r\n", idx, processNames[idx-1], vmi.cbRwMemUsed ));
			        System.Diagnostics.Debug.WriteLine( String.Format("\r\n{0} ({1}): {2} bytes", idx, processNames[idx-1], vmi.cbRwMemUsed ));
			        total += vmi.cbRwMemUsed;
                    procVMinfoList.Add(new procVMinfo(processNames[idx - 1], vmi.cbRwMemUsed, (byte)(idx)));
		        }
	        }
	        str.Append(String.Format("Total: {0} bytes\r\n", total ));
            System.Diagnostics.Debug.WriteLine( String.Format("Total: {0} bytes\r\n", total ));
	        return str;
        }
    }
}

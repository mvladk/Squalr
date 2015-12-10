﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Binarysharp.MemoryManagement;

namespace Anathema
{
    /* In this class we fetch a process and store it in the target process passed by reference. The method of grabbing
     * processes and sorting them based on time since execution is as follows:
     * 1) Grab all available processes
     * 2) Sort them into two categories -- 'session0' (important system processes) and 'standard'.
     * It is worth noting here that we cannot access the 'time since execution' for 'session0' unless anathema is running
     * as admin. Trying to access the time results in errors and creates noticable overhead with a try/catch statement,
     * thus we have to sort them into the two formerly mentioned categories in advanced, and only fetch icons
     * for those in the 'standard' category.
     * 3) Sort the 'standard' list based on time since execution, and the 'session0' list based on processID
     * 4) Merge lists into one list, placing the 'standard' before the 'session0'.
     * 5) Loop over the 'standard' portion of the list, fetching icons.
     * Here it is also worth noting that there are issues trying to access an icon of a 64-bit process from a 32-bit
     * version of A. If we are 64-bit, we call a function that doesn't have to worry about this stuff. If we are
     * 32-bit, again try/catches again create too much overhead, so we use the function IsWow64Process to determine if
     * each process is compatable (also 32-bit), and if so THEN we can make a proper request.
     * 6) Update the target process in the static class TargetProcess
     * 
     * -------------------------------------------------------------------------------
     * 
     * Further implamentations:
     * - Icon fetching for session0 items (~3-5 have icons)
     */
    class ProcessSelector : IProcessSelectorModel
    {
        // Singleton instance of the process selector. There is no reason to have more than one of these active at once.
        private static ProcessSelector _ProcessSelector;

        // Complete list of running processes
        private List<Process> ProcessList;

        public event ProcessSelectorEventHandler EventDisplayProcesses;
        public event ProcessSelectorEventHandler EventSelectProcess;

        // Observers that must be notified of a process selection change
        private List<IProcessObserver> ProcessObservers;

        private ProcessSelector()
        {
            ProcessObservers = new List<IProcessObserver>();
        }

        public static ProcessSelector GetInstance()
        {
            if (_ProcessSelector == null)
                _ProcessSelector = new ProcessSelector();

            return _ProcessSelector;
        }

        public void Subscribe(IProcessObserver Observer)
        {
            if (ProcessObservers.Contains(Observer))
                return;
            
            ProcessObservers.Add(Observer);
        }

        public void Unsubscribe(IProcessObserver Observer)
        {
            if (!ProcessObservers.Contains(Observer))
                return;

            ProcessObservers.Remove(Observer);
        }

        public void Notify(Process Process)
        {
            MemorySharp MemoryEditor = new MemorySharp(Process);
            for (Int32 Index = 0; Index < ProcessObservers.Count; Index++)
                ProcessObservers[Index].UpdateMemoryEditor(MemoryEditor);
        }

        public void SelectProcess(Int32 Index)
        {
            // Check if this is a valid index into the process list
            if (ProcessList == null || Index < 0 || Index >= ProcessList.Count)
                return;

            // Raise event that we have selected a process
            ProcessSelectorEventArgs ProcessSelectorEventArgs = new ProcessSelectorEventArgs();
            ProcessSelectorEventArgs.SelectedProcess = ProcessList[Index];
            EventSelectProcess(this, ProcessSelectorEventArgs);

            Notify(ProcessList[Index]);
        }

        public void RefreshProcesses(IntPtr ProcessSelectorHandle)
        {
            ProcessSelectorEventArgs ProcessSelectorEventArgs = new ProcessSelectorEventArgs();
            List<Process> StandardProcessList;

            // Get the process list
            ProcessList = FetchAllProcesses(out StandardProcessList);
            ProcessSelectorEventArgs.ProcessList = ProcessList;

            // Grab icons for the just the standard processes (system processes tend not to allow this and are slow)
            ProcessSelectorEventArgs.ProcessIcons = FetchIcons(ProcessSelectorHandle, StandardProcessList);

            // Signal the new lists to the presenter
            EventDisplayProcesses(this, ProcessSelectorEventArgs);
        }

        // Determines if Anathema is able to perform certain actions on the target process, such as fetching icons
        public static Boolean IsProcessOSCompatable(IntPtr ProcessHandle)
        {
            if (IsAnthema64Bit())
                return true;

            if (IsProcess64Bit(ProcessHandle))
                return true;

            // Target uses higher addressing than Anathema, thus Anathema is not compatable
            return false;
        }

        // Determines if a process is a session0 or not and adds it to the appropriate list
        private List<Process> FetchAllProcesses(out List<Process> StandardProcessList)
        {
            List<Process> UnsortedProcesses = new List<Process>(Process.GetProcesses());
            ConcurrentBag<Process> SystemProcessBag = new ConcurrentBag<Process>(); // Important system processes
            ConcurrentBag<Process> StandardProcessBag = new ConcurrentBag<Process>(); // Generic processes

            // Fetch processes in parallel
            //Parallel.For(0, UnsortedProcesses.Count, ProcessIndex =>
            for (int ProcessIndex = 0; ProcessIndex < UnsortedProcesses.Count; ProcessIndex++)
            {
                // Guarenteed session0, but misses some things
                if (UnsortedProcesses[ProcessIndex].SessionId == 0) // NO possible access violation
                    SystemProcessBag.Add(UnsortedProcesses[ProcessIndex]);

                // This seems to grab only system processes. This is semi-incorrect since
                // that doesn't have to be true, but it seems to always be the case.
                else if (UnsortedProcesses[ProcessIndex].BasePriority == 13) // NO possible access violation
                {
                    SystemProcessBag.Add(UnsortedProcesses[ProcessIndex]);
                }
                else
                {
                    try
                    {
                        // This boolean will throw an error if it is a Session0 process
                        if (UnsortedProcesses[ProcessIndex].PriorityBoostEnabled) // POSSIBLE access violation
                            StandardProcessBag.Add(UnsortedProcesses[ProcessIndex]);
                    }
                    catch (Win32Exception) // Session0 access denied error
                    {
                        SystemProcessBag.Add(UnsortedProcesses[ProcessIndex]); // Collect any missed session0 targets
                    }
                    catch (InvalidOperationException)
                    {

                    }
                }
            }//);

            // Convert concurrent bags to lists
            List<Process> SystemProcessList = new List<Process>(SystemProcessBag);
            StandardProcessList = new List<Process>(StandardProcessBag);

            // Sort the lists
            StandardProcessList.Sort(ProcessTimeComparer.Default);  // Sort our standard list based on execution time
            SystemProcessList.Sort(ProcessIDComparer.Default);      // Sort session0 items by ID

            // Combine the lists
            List<Process> ProcessList = new List<Process>();
            ProcessList.AddRange(StandardProcessList); // Start by copying all standard processes first
            ProcessList.AddRange(SystemProcessList);   // Copy in session0 / system processes last

            return ProcessList;
        }

        // Process icon fetching optimized for AE 32-bit on a 64-bit OS
        List<Icon> FetchIcons(IntPtr ProcessSelectorHandle, List<Process> ProcessList)
        {
            // Icons to correspond to our processes
            List<Icon> ImageList = new List<Icon>();

            // Try to grab icons for the main list only
            for (Int32 ProcessIndex = 0; ProcessIndex < ProcessList.Count; ProcessIndex++)
                ImageList.Add(GetIcon(ProcessSelectorHandle, ProcessList[ProcessIndex]));

            return ImageList;
        }

        private Icon GetIcon(IntPtr ProcessSelectorHandle, Process TargetProcess)
        {
            try
            {
                // 32-bit OS grabbing 64-bit icons isn't allowed, so we check
                if (!IsProcessOSCompatable(TargetProcess.Handle))
                    return null;

                IntPtr IconHandle = ExtractIcon(ProcessSelectorHandle, TargetProcess.MainModule.FileName, 0);

                if (!IconHandle.Equals(IntPtr.Zero))
                    return (Icon.FromHandle(IconHandle));
            }
            catch { }

            return null;
        }

        public static bool IsProcess64Bit(IntPtr ProcessHandle)
        {
            // First do the simple check if seeing if the OS is 32 bit, in which case the process wont be 64 bit
            if (!IsOS64Bit())
                return false;

            // OS is 64 bit. Must determine if target is 32 bit or 64 bit.
            bool Result;
            IsWow64Process(ProcessHandle, out Result);
            return Result;
        }

        /// <summary>
        /// Determines if the OS is 32 bit or 64 bit windows
        /// </summary>
        /// <returns></returns>
        public static bool IsOS64Bit()
        {
            return Environment.Is64BitOperatingSystem;
        }

        /// <summary>
        /// Determines if Anathema is running as 32 bit or 64 bit
        /// </summary>
        /// <returns></returns>
        public static bool IsAnthema64Bit()
        {
            return Environment.Is64BitProcess;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr ProcessHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool Wow64Process);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    }

    #region Process comparer classes

    // Class that can sort processes by time since execution
    class ProcessTimeComparer : IComparer<Process>
    {
        public static readonly ProcessTimeComparer Default = new ProcessTimeComparer();
        public ProcessTimeComparer() { }

        public int Compare(Process ProcessA, Process ProcessB)
        {
            return DateTime.Compare(ProcessB.StartTime, ProcessA.StartTime);
        }
    }

    // Class that can sort processes by ID
    class ProcessIDComparer : IComparer<Process>
    {
        public static readonly ProcessIDComparer Default = new ProcessIDComparer();
        public ProcessIDComparer() { }

        public int Compare(Process ProcessA, Process ProcessB)
        {
            if (ProcessA.Id < ProcessB.Id)
                return 1;
            else if (ProcessA.Id > ProcessB.Id)
                return -1;
            else
                return 0;
        }
    }

    #endregion
}
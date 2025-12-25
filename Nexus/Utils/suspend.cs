// Copyright (c) 2025 harharud
// Licensed under the Apache License 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Suspend
{
    internal static class proc
    {

        public static void DownloadFile(string Url, string Path)
        {
            (new WebClient()).DownloadFile(Url, Path);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr OpenThread(proc.ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);


        public static void Resume(this Process process)
        {
            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr intPtr = proc.OpenThread(proc.ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (intPtr == IntPtr.Zero)
                {
                    break;
                }
                else
                {
                    proc.ResumeThread(intPtr);
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern int ResumeThread(IntPtr hThread);

        public static void Suspend(this Process process)
        {
            foreach (ProcessThread thread in (ReadOnlyCollectionBase)process.Threads)
            {
                IntPtr intPtr = proc.OpenThread(proc.ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (intPtr == IntPtr.Zero)
                {
                    break;
                }
                else
                {
                    proc.SuspendThread(intPtr);
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern uint SuspendThread(IntPtr hThread);

        [Flags]
        public enum ThreadAccess
        {
            TERMINATE = 1,
            SUSPEND_RESUME = 2,
            GET_CONTEXT = 8,
            SET_CONTEXT = 16,
            SET_INFORMATION = 32,
            QUERY_INFORMATION = 64,
            SET_THREAD_TOKEN = 128,
            IMPERSONATE = 256,
            DIRECT_IMPERSONATION = 512
        }


        public static void InjectDll(int pid, string path)
        {
            UIntPtr uIntPtr;
            IntPtr intPtr = OpenProcess(1082, false, pid);
            IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            uint length = (uint)((path.Length + 1) * Marshal.SizeOf(typeof(char)));
            IntPtr intPtr1 = VirtualAllocEx(intPtr, IntPtr.Zero, length, 12288, 4);
            WriteProcessMemory(intPtr, intPtr1, Encoding.Default.GetBytes(path), length, out uIntPtr);
            CreateRemoteThread(intPtr, IntPtr.Zero, 0, procAddress, intPtr1, 0, IntPtr.Zero);
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
    }

}


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace ExportSrc
{
    public class ReparsePoint
    {
        private const Int32 INVALID_HANDLE_VALUE = -1;
        private const Int32 OPEN_EXISTING = 3;
        private const Int32 FILE_FLAG_OPEN_REPARSE_POINT = 0x200000;
        private const Int32 FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
        private const Int32 FSCTL_GET_REPARSE_POINT = 0x900A8;

        /// <summary>
        /// If the path "REPARSE_GUID_DATA_BUFFER.SubstituteName" 
        /// begins with this prefix,
        /// it is not interpreted by the virtual file system.
        /// </summary>
        private const String NonInterpretedPathPrefix = "\\??\\";
        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_GUID_DATA_BUFFER
        {
            public UInt32 ReparseTag;
            public UInt16 ReparseDataLength;
            public UInt16 Reserved;
            public UInt16 SubstituteNameOffset;
            public UInt16 SubstituteNameLength;
            public UInt16 PrintNameOffset;
            public UInt16 PrintNameLength;

            /// <summary>
            /// Contains the SubstituteName and the PrintName.
            /// The SubstituteName is the path of the target directory.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(String lpFileName,
                                                Int32 dwDesiredAccess,
                                                Int32 dwShareMode,
                                                IntPtr lpSecurityAttributes,
                                                Int32 dwCreationDisposition,
                                                Int32 dwFlagsAndAttributes,
                                                IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Int32 CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Int32 DeviceIoControl(IntPtr hDevice,
                                                     Int32 dwIoControlCode,
                                                     IntPtr lpInBuffer,
                                                     Int32 nInBufferSize,
                                                     IntPtr lpOutBuffer,
                                                     Int32 nOutBufferSize,
                                                     out Int32 lpBytesReturned,
                                                     IntPtr lpOverlapped);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool CreateSymbolicLink(
            string lpSymlinkFileName,
            string lpTargetFileName,
            SymbolicLinkType dwFlags
        );


        private bool CreateHardLink(string lpFileName, string lpExistingFileName)
        {
            return CreateHardLink(lpFileName, lpExistingFileName, IntPtr.Zero);
        }

        public static bool IsSymbolicLink(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    return true;
                }
            }

            if (File.Exists(path) && ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint))
            {
                return true;
            }


            return false;
        }

        /// <summary>
        /// Gets the target directory from a directory link in Windows Vista.
        /// </summary>
        /// <param name="directoryInfo">The directory info of this directory 
        /// link</param>
        /// <returns>the target directory, if it was read, 
        /// otherwise an empty string.</returns>
        public static String GetTargetDir(FileSystemInfo directoryInfo)
        {
            String targetDir = string.Empty;

            try
            {
                // Is it a directory link?
                if ((directoryInfo.Attributes
                    & FileAttributes.ReparsePoint) != 0)
                {
                    // Open the directory link:
                    IntPtr hFile = CreateFile(directoryInfo.FullName,
                                                0,
                                                0,
                                                IntPtr.Zero,
                                                OPEN_EXISTING,
                                                FILE_FLAG_BACKUP_SEMANTICS |
                                                FILE_FLAG_OPEN_REPARSE_POINT,
                                                IntPtr.Zero);
                    if (hFile.ToInt32() != INVALID_HANDLE_VALUE)
                    {
                        // Allocate a buffer for the reparse point data:
                        Int32 outBufferSize = Marshal.SizeOf(typeof(REPARSE_GUID_DATA_BUFFER));
                        IntPtr outBuffer = Marshal.AllocHGlobal(outBufferSize);

                        try
                        {
                            // Read the reparse point data:
                            Int32 bytesReturned;
                            Int32 readOK = DeviceIoControl(hFile,
                                                       FSCTL_GET_REPARSE_POINT,
                                                            IntPtr.Zero,
                                                            0,
                                                            outBuffer,
                                                            outBufferSize,
                                                            out bytesReturned,
                                                            IntPtr.Zero);
                            if (readOK != 0)
                            {
                                // Get the target directory from the reparse 
                                // point data:
                                REPARSE_GUID_DATA_BUFFER rgdBuffer =
                                    (REPARSE_GUID_DATA_BUFFER)
                                    Marshal.PtrToStructure
                                 (outBuffer, typeof(REPARSE_GUID_DATA_BUFFER));
                                targetDir = Encoding.Unicode.GetString
                                          (rgdBuffer.PathBuffer,
                                          rgdBuffer.SubstituteNameOffset,
                                          rgdBuffer.SubstituteNameLength);
                                if (targetDir.StartsWith
                                    (NonInterpretedPathPrefix))
                                {
                                    targetDir = targetDir.Substring
                        (NonInterpretedPathPrefix.Length);
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }

                        // Free the buffer for the reparse point data:
                        Marshal.FreeHGlobal(outBuffer);

                        // Close the directory link:
                        CloseHandle(hFile);
                    }
                }
            }
            catch (Exception)
            {
            }

            return targetDir;
        }
    }
}

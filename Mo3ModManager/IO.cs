using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Mo3ModManager
{
    static class IO
    {
        /// <summary>
        /// Удаляет все содержимое каталога.
        /// </summary>
        /// <param name="Directory">Каталог для очистки</param>
        public static void ClearDirectory(string Directory)
        {

            var directoryInfo = new DirectoryInfo(Directory);
            foreach (var file in directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                file.Delete();
            }
            foreach (var folder in directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly))
            {
                folder.Delete(true);
            }
        }
        /// <summary>
        /// Определяет, являются ли два файла на самом деле одинаковыми.
        /// Примечание: в файловой системе ReFS этот метод может работать некорректно. Для поддержки ReFS необходимо использовать GetFileInformationByHandleEx().
        /// </summary>
        /// <param name="FileA">Путь к файлу A.</param>
        /// <param name="FileB">Путь к файлу B</param>
        /// <returns></returns>
        public static bool IsSameFile(string FileA, string FileB)
        {
            IntPtr fileAHandle = Win32.NativeMethods.CreateFileW(
                FileA,
                Win32.NativeConstants.GENERIC_READ,
                Win32.NativeConstants.FILE_SHARE_READ | Win32.NativeConstants.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.NativeConstants.OPEN_EXISTING,
                Win32.NativeConstants.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero
                );

            if (fileAHandle == Win32.NativeConstants.INVALID_HANDLE_VALUE)
            {
                Trace.WriteLine("[Предупреждение] Не удалось открыть файл " + FileA + ". Ошибка " + Win32.NativeMethods.GetLastError() + ".");
                return false;
            }

            IntPtr fileBHandle = Win32.NativeMethods.CreateFileW(
                FileB,
                Win32.NativeConstants.GENERIC_READ,
                Win32.NativeConstants.FILE_SHARE_READ | Win32.NativeConstants.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.NativeConstants.OPEN_EXISTING,
                Win32.NativeConstants.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero
                );

            if (fileBHandle == Win32.NativeConstants.INVALID_HANDLE_VALUE)
            {
                Trace.WriteLine("[Предупреждение] Не удалось открыть файл " + FileB + ". Ошибка " + Win32.NativeMethods.GetLastError() + ".");
                return false;
            }

            if (!Win32.NativeMethods.GetFileInformationByHandle(fileAHandle, out var fileAInfo))
            {
                Trace.WriteLine("[Предупреждение] Не удалось получить информацию о файле " + FileA + ". Ошибка " + Win32.NativeMethods.GetLastError() + ".");
                return false;
            }

            if (!Win32.NativeMethods.GetFileInformationByHandle(fileBHandle, out var fileBInfo))
            {
                Trace.WriteLine("[Предупреждение] Не удалось получить информацию о файле " + FileB + ". Ошибка " + Win32.NativeMethods.GetLastError() + ".");
                return false;
            }

            Win32.NativeMethods.CloseHandle(fileAHandle);
            Win32.NativeMethods.CloseHandle(fileBHandle);

            return (fileAInfo.dwVolumeSerialNumber == fileBInfo.dwVolumeSerialNumber) && (fileAInfo.nFileIndexHigh == fileBInfo.nFileIndexHigh) && (fileAInfo.nFileIndexLow == fileBInfo.nFileIndexLow);

        }


        /// <summary>
        /// Удаляет все пустые подпапки.
        /// </summary>
        /// <param name="directory">Папка</param>
        /// <returns> Была ли папка удалена или нет.</returns>
        public static bool RemoveEmptyFolders(string directory)
        {
            bool status = true;
            foreach (var folder in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                bool result = RemoveEmptyFolders(folder);
                status = status && result;
            }

            if (status && (Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).Count() == 0))
            {
                Directory.Delete(directory);
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Создает жесткие ссылки для всех файлов, как при копировании папок.
        /// </summary>
        /// <param name="SrcDirectory">Исходная папка.</param>
        /// <param name="DestDirectory">Целевая папка.</param>
        public static void CreateHardLinksOfFiles(string SrcDirectory, string DestDirectory)
        {
            CreateHardLinksOfFiles(SrcDirectory, DestDirectory, false, null);
        }
        /// <summary>
        /// Создает жесткие ссылки для всех файлов, как при копировании папок.
        /// </summary>
        /// <param name="SrcDirectory">Исходная папка.</param>
        /// <param name="DestDirectory">Целевая папка.</param>
        /// <param name="Override">Перезаписывать ли файлы, если они существуют</param>
        public static void CreateHardLinksOfFiles(string SrcDirectory, string DestDirectory, bool Override)
        {
            CreateHardLinksOfFiles(SrcDirectory, DestDirectory, Override, null); // Передаем null для skipExtensionsWithDotUpper
        }
        public static void CreateHardLinksOfFiles(string SrcDirectory, string DestDirectory, bool Override, List<string> skipExtensionsWithDotUpper)
        {
            Debug.WriteLine("Источник: " + SrcDirectory);
            // Создать все папки
            foreach (string subDirectory in Directory.GetDirectories(SrcDirectory, "*", SearchOption.AllDirectories))
            {
                string relativeName = subDirectory.Substring(SrcDirectory.Length + 1);
                // независимо от того, существует ли эта папка, следующий метод не выдаст исключение
                Directory.CreateDirectory(Path.Combine(DestDirectory, relativeName));

                Debug.WriteLine("Создан каталог: " + relativeName);
            }

            // Создать жесткую ссылку NTFS, если не существует
            foreach (string srcFullName in Directory.GetFiles(SrcDirectory, "*", SearchOption.AllDirectories))
            {
                string relativeName = srcFullName.Substring(SrcDirectory.Length + 1);
                string destFullName = Path.Combine(DestDirectory, relativeName);

                CreateHardLinkOrCopy(destFullName, srcFullName, Override, skipExtensionsWithDotUpper);
                //if (!File.Exists(destFullName))
                //{
                //    Win32.NativeMethods.CreateHardLinkW(destFullName, srcFullName, IntPtr.Zero);
                //}
                //else
                //{
                //    if (Override)
                //    {
                //        // Файл существует
                //        Debug.WriteLine("Перезаписан: " + relativeName);
                //        File.Delete(destFullName);
                //        Win32.NativeMethods.CreateHardLinkW(destFullName, srcFullName, IntPtr.Zero);
                //    }
                //}
            }
        }

        private static void CreateHardLinkOrCopy(string destFile, string srcFile, bool Override, List<string> skipExtensionsWithDot)
        {
            var ext = Path.GetExtension(destFile).ToUpperInvariant();
            if (File.Exists(destFile) && Override)
            {
                Debug.WriteLine("Перезаписан: " + destFile);
                File.Delete(destFile);
            }
            
            if (!File.Exists(destFile)) {

                if (skipExtensionsWithDot != null && skipExtensionsWithDot.Contains(ext))
                {
                    File.Copy(srcFile, destFile, Override);
                }
                else
                {
                    Win32.NativeMethods.CreateHardLinkW(destFile, srcFile, IntPtr.Zero);
                }
            } 
                
        }

    }
}

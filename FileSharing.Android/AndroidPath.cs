using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using FileSharing;
using FileSharing.Droid;

[assembly: Xamarin.Forms.Dependency(typeof(AndroidPath))]
namespace FileSharing.Droid
{
    public class AndroidPath : IPlatformPath
    {
        public string DownloadPath()
        {
            return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
        }
    }
}
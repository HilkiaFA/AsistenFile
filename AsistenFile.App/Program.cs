using System;
using System.Windows.Forms;
using AsistenFile.App.UI;

namespace AsistenFile.App
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}

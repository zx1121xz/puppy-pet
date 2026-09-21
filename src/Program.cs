using System;
using System.Threading;
using System.Windows.Forms;

namespace PuppyPet
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "PuppyPet_SingleInstance_v1", out created))
            {
                if (!created) return;   // 只允许一只小狗在桌面上

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new PetForm());
            }
        }
    }
}

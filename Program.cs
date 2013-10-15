using System.ServiceProcess;
using System.Windows.Forms;

namespace GacInstaller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            bool _IsInstalled = false;
            bool serviceStarting = false; // Thanks to SMESSER's implementation V2.0
            string SERVICE_NAME = "GacInstaller";

            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController service in services)
            {
                if (service.ServiceName.Equals(SERVICE_NAME))       
                {   
                    _IsInstalled = true;          
                    if(service.Status == ServiceControllerStatus.StartPending)          
                    {             
                        // If the status is StartPending then the service was started via the SCM             
                        serviceStarting = true;          
                    }          
                    break;       
                }
            }

            if (!serviceStarting)
            {
                if (_IsInstalled == true)
                {
                    // Thanks to PIEBALDconsult's Concern V2.0
                    DialogResult dr = new DialogResult();
                    dr = MessageBox.Show("Do you REALLY like to uninstall the " + SERVICE_NAME + "?", "Danger", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Yes)
                    {
                        SelfInstaller.Uninstall();
                        MessageBox.Show("Successfully uninstalled the " + SERVICE_NAME, "Status",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    DialogResult dr = new DialogResult();
                    dr = MessageBox.Show("Do you REALLY like to install the " + SERVICE_NAME + "?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Yes)
                    {
                        SelfInstaller.Install();
                        MessageBox.Show("Successfully installed the " + SERVICE_NAME, "Status",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            else
            {   
                // Started from the SCM
                System.ServiceProcess.ServiceBase[] servicestorun;
                servicestorun = new System.ServiceProcess.ServiceBase[] { new GacInstaller(),  };
                ServiceBase.Run(servicestorun);
            }
        }

        }
}

using OGVColorCatcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    public class SetupService
    {
        I1SharpModel model = new I1SharpModel();
        public SetupService(I1SharpModel model) { 
            this.model = model;
        }

        public void DotValueSetup()
        {
            // Method implementation goes here
        }

        public void CurveDotValueSetup()
        {
            // Method implementation goes here
        }

        public void DensitySetup()
        {
            this.model.CurrentDevice.SetAbsolutePaper();
        }
    }
}

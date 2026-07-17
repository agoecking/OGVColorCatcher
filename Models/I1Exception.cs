using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Models
{
    public class I1Exception : Exception
    {
        /// <summary>
        /// Get the error code
        /// </summary>
        public I1Pro64.Result Result { get; private set; }

        /// <summary>
        /// Create an I1Exception
        /// </summary>
        /// <param name="result">The result value returned from the C-SDK</param>
        /// <param name="message">The error message assigned to the error code</param>
        public I1Exception(I1Pro64.Result result, string message)
        : base(message)
        {
            Result = result;
        }

        /// <summary>
        /// Error message 
        /// </summary>
        public override string Message
        {
            get
            {
                return "Error " + (int)Result + " (" + Result + "): " + base.Message;
            }
        }
    }
}

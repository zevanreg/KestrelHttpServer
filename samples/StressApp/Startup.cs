using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Server.Kestrel;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.Framework.Logging;
using System.Text;

namespace StressApp
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            var ksi = app.ServerFeatures[typeof(IKestrelServerInformation)] as IKestrelServerInformation;
            ksi.ThreadCount = 4;

            var helloWorld = Encoding.ASCII.GetBytes("Hello World");
            app.Run(context =>
            {
                context.Response.Headers.Remove("Date");
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength = helloWorld.Length;
                context.Response.Body.Write(helloWorld, 0, helloWorld.Length);
                return TaskUtilities.CompletedTask;
            });
        }
    }
}

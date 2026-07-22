using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SchedulerP360Insight.Observability
{
    public sealed class JsonHealthFilePublisher : IHealthPublisher
    {
        private readonly string targetPath;
        private readonly object publishLock = new object();
        private readonly JsonSerializerOptions serializerOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

        public JsonHealthFilePublisher(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException(
                    "La ruta de salud es obligatoria.",
                    nameof(targetPath));
            }

            this.targetPath = Path.GetFullPath(targetPath);
        }

        public bool Enabled => true;

        public void Publish(HealthSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            string temporaryPath = targetPath + "." +
                snapshot.ProcessId + ".tmp";
            string json = JsonSerializer.Serialize(snapshot, serializerOptions);

            lock (publishLock)
            {
                try
                {
                    using (FileStream stream = new FileStream(
                        temporaryPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    using (StreamWriter writer = new StreamWriter(
                        stream,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                    {
                        writer.Write(json);
                    }

                    if (File.Exists(targetPath))
                    {
                        File.Replace(temporaryPath, targetPath, null);
                    }
                    else
                    {
                        File.Move(temporaryPath, targetPath);
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }
    }

    public sealed class NullHealthPublisher : IHealthPublisher
    {
        public static readonly NullHealthPublisher Instance =
            new NullHealthPublisher();

        private NullHealthPublisher()
        {
        }

        public bool Enabled => false;

        public void Publish(HealthSnapshot snapshot)
        {
        }
    }
}

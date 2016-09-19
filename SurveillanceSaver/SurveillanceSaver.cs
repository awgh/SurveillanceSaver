// SurveillanceSaver by awgh@awgh.org
// Stylized Eye icon -> By camelNotation - Own work, CC0, https://commons.wikimedia.org/w/index.php?curid=29553676

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Text;
using Screensavers;
using System.Drawing.Drawing2D;
using System.Xml;
using System.Collections;
using System.Net.Http;
using System.IO;
using System.Net;
using System.Reflection;

namespace SurveillanceSaver
{
    class SurveillanceSaver : Screensaver
    {
        int[] rolls;
        int[] rollsCache;

        ArrayList links = new ArrayList();
        Hashtable decoders = new Hashtable();
        Hashtable decoderToIndex = new Hashtable();
        Bitmap[] images;
        Random rand = new Random();
        HttpClient httpClient = new HttpClient();
        int ticks = 0;

        public SurveillanceSaver()
            : base(FullscreenMode.MultipleWindows)
        {
            this.Initialize += new EventHandler(SurveillanceSaver_Initialize);
            this.Update += new EventHandler(SurveillanceSaver_Update);
            this.OneSecondTick += SurveillanceSaver_OneSecondTick;

            this.SettingsText = "awgh@awgh.org";
        }


        private void SurveillanceSaver_OneSecondTick(object sender, EventArgs e)
        {
            int n = 120;
            // reroll everything in a staggered way
            for (int i = 0; i < rolls.Length; i++)
            {
                if (ticks % n == i)
                    rolls[i] = rand.Next(0, links.Count);
            }
            ticks++;
        }

        [STAThread]
        static void Main()
        {
            // no SSL validation.  don't be scared, it's alright
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, cert, chain, sslPolicyErrors) => true;

            SurveillanceSaver ps = new SurveillanceSaver();
            ps.Run();
        }

        void SurveillanceSaver_Update(object sender, EventArgs e)
        {
            DoUpdate();
            DoRender();
        }

        async void DoUpdate()
        {
            int index = 0;
            foreach (Window win in Windows)
            {
                int r = rolls[index];
                string mime = ((Link)links[r]).mime;
                if (mime.Contains("="))  // it's an actual MJPEG stream
                {
                    if (!decoders.ContainsKey(r))
                    {
                        MjpegDecoder mjpeg = new MjpegDecoder();
                        mjpeg.FrameReady += mjpeg_FrameReady;
                        decoders[r] = mjpeg;
                        decoderToIndex[mjpeg] = index;
                    }
                    ((MjpegDecoder)decoders[r]).ParseStream(new Uri(((Link)links[r]).href));
                }
                else  // it's just a JPEG
                {
                    try
                    {
                        var httpClient = new HttpClient();
                        var content = await httpClient.GetStreamAsync(((Link)links[r]).href);
                        Bitmap b = null;
                        lock (images)
                        {
                            try
                            {
                                b = new Bitmap(content);
                            }
                            catch (IOException e)
                            {
                                // read failed for some reason... reroll?
                                Console.WriteLine("Jpeg Fetch Error:\n" + e.ToString());
                            }
                            if (b != null)
                            {
                                lock (images)
                                {
                                    images[index] = b;
                                    rollsCache[index] = rolls[index];
                                }
                            }
                        }
                    }
                    catch (HttpRequestException e)
                    {
                        //reroll
                        //rolls[index] = rand.Next(0, links.Count);                        
                        Console.WriteLine("Jpeg Fetch Error:\n" + e.ToString());
                    }
                }

                index++;
            }
        }

        private void mjpeg_FrameReady(object sender, FrameReadyEventArgs e)
        {
            if (decoderToIndex.ContainsKey(sender))
            {
                lock (images)
                {
                    int index = (int)decoderToIndex[sender];
                    images[index] = new Bitmap(e.Bitmap);
                    rollsCache[index] = rolls[index];
                }
            }
        }

        void DoRender()
        {
            int index = 0;
            foreach (Window win in Windows)
            {
                Graphics g = win.Graphics;
                float width = g.VisibleClipBounds.Width;
                float height = g.VisibleClipBounds.Height;
                g.InterpolationMode = InterpolationMode.High;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                lock (images)
                {
                    Bitmap image = (Bitmap)images[index];
                    g.Clear(Color.Black);
                    if (image != null)
                    {
                        float scale = Math.Min(width / image.Width, height / image.Height);
                        var scaleWidth = (int)(image.Width * scale);
                        var scaleHeight = (int)(image.Height * scale);

                        g.DrawImage(image, new Rectangle(((int)width - scaleWidth) / 2, ((int)height - scaleHeight) / 2,
                            scaleWidth, scaleHeight));

                        Link l = (Link)links[(int)rollsCache[index]];
                        String s = "";
                        if (l.href != null)
                        {
                            if (l.city != null)
                                s += l.city + ",\n";
                            if (l.region != null)
                            {
                                s += l.region + ",\n";
                            }
                            if (l.country != null)
                            {
                                s += l.country + "\n\n";
                            }
                            if (l.ip != null)
                            {
                                s += l.ip + "\n\n";
                            }
                            if (l.lat != null)
                            {
                                s += l.lat + ",\n";
                            }
                            if (l.lng != null)
                            {
                                s += l.lng + "\n";
                            }
                        }
                        g.DrawString(s, new Font(FontFamily.GenericSerif, 16), Brushes.AntiqueWhite, 10, 30, StringFormat.GenericDefault);
                    }
                }
                index++;
            }
        }

        struct Link
        {
            public string href;
            public string mime;
            public string city;
            public string region;
            public string country;
            public string ip;
            public string lat;
            public string lng;
        }

        void SurveillanceSaver_Initialize(object sender, EventArgs e)
        {     
            Assembly a = Assembly.GetExecutingAssembly();
            Stream stream = a.GetManifestResourceStream("SurveillanceSaver.urls.xml");
            XmlReader reader = XmlReader.Create(stream);

            reader.MoveToContent();
            reader.Read();
            while (!reader.EOF)
            {
                if (reader.IsStartElement())
                {
                    Link l = new Link();
                    l.href = reader.GetAttribute("link");
                    l.mime = reader.GetAttribute("mime");
                    l.city = reader.GetAttribute("city");
                    l.region = reader.GetAttribute("region");
                    l.country = reader.GetAttribute("country");
                    l.ip = reader.GetAttribute("ip");
                    l.lat = reader.GetAttribute("lat");
                    l.lng = reader.GetAttribute("long");
                    links.Add(l);
                }
                reader.Read();
            }
            reader.Close();

            rolls = new int[Windows.Count];
            for (int i = 0; i < Windows.Count; i++)
            {
                rolls[i] = rand.Next(0, links.Count);
            }
            rollsCache = (int[])rolls.Clone();
            images = new Bitmap[Windows.Count];

            DoUpdate();
        }
    }
}

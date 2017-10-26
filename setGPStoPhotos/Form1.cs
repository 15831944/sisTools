using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace setGPStoPhotos
{
    public partial class Form1 : Form
    {
        #region 私有字段
        // 输入信息列表;     key:name     经   纬   高   焦
        Dictionary<string, double[]> info_list;
        // 输入文件
        string[] fileList;
        //暂定经度为E(东经)   longitude 
        char chrGPSLongitudeRef = 'E';
        //暂定纬度N(北纬) latitude 
        char chrGPSLatitudeRef = 'N';
        int i;
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("此操作将修改照片属性，不可逆，建议先测试或备份原片。确定要开始修改？", "友情提示", MessageBoxButtons.YesNo) == DialogResult.Yes
                && getInfoList() && getFileList())
            {
                new Thread(new ThreadStart(() =>
                {
                    for (i = 0; i < fileList.Length; i++)
                    {
                        //打开图像，获取属性
                        Image bmp = Image.FromFile(fileList[i]);
                        List<PropertyItem> ProItems = bmp.PropertyItems.ToList();

                        //拿到属性后可以释放了
                        bmp.Dispose();

                        //修改标记，没有改动则不会重写文件
                        bool needWrite = false;

                        //写入指定信息
                        startUP(fileList[i], ref ProItems, ref needWrite);

                        //修改完成，判断是否要重写文件
                        if (needWrite)
                            WriteNewDescriptionInImage(fileList[i], ProItems);
                    }
                })).Start();
                button1.Enabled = false;
                timer1.Start();
            }
            else
            {
                MessageBox.Show("输入有误，姐需要重新指定文件！");
            }
        }
        private void label2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("这个工具可以帮你修改照片中的GPS和焦距属性（EXIF）。需要一个TXT的示例文件吗？", "Help", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                string[] lines = {  "照片名	经度	纬度	海拔高度	焦距(如果不需要，可以删掉这列)",
                                    "DSC00153.JPG	11.1961596	48.0851636	619.816	16.2",
                                    "DSC00154.JPG	11.1961175	48.0851414	619.424	16.2",
                                    "..." };
                try
                {
                    File.WriteAllLines(@"示例.txt", lines, Encoding.UTF8);
                    if (File.Exists(@"示例.txt"))
                        Process.Start(@"示例.txt");
                }
                catch { MessageBox.Show("写文件失败，我可能无权在这里创建文件。"); }
            }
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (progressBar1.Value < 100)
            {
                progressBar1.Value = (i + 1) * 100 / fileList.Length;
                label1.Text = string.Format("{0} / {1}", i + 1, fileList.Length);
            }
            else
            {
                timer1.Stop();
                if (MessageBox.Show("完成!") == DialogResult.OK)
                {
                    button1.Enabled = true;
                    progressBar1.Value = 0;
                    label1.Text = "";
                }
            }
        }
        /// <summary>
        /// 获取信息列表
        /// </summary>
        /// <returns></returns>
        private bool getInfoList()
        {
            if (openTXTFile.ShowDialog() == DialogResult.OK)
            {
                info_list = new Dictionary<string, double[]>();
                string[] allLines = File.ReadAllLines(openTXTFile.FileName);
                //遍历读进来的信息
                for (int i = 0; i < allLines.Length; i++)
                {
                    //拆分
                    string[] itms = allLines[i].Split('\t');
                    if (itms.Length > 3)
                    {
                        //参数个数，带焦距是4个，不带是3个
                        int parmasCount = itms.Length - 1;

                        //第一列是照片名
                        string ky = itms[0].Trim();

                        //后面的列依次为：经   纬   高   [焦]
                        double[] vals = new double[parmasCount];

                        double t;
                        for (int j = 0; j < parmasCount; j++)
                            vals[j] = double.TryParse(itms[j + 1].Trim(), out t) ? t : double.NaN;
                        info_list.Add(ky, vals);
                    }
                    else continue;
                }
                return true;
            }
            else
                return false;
        }
        /// <summary>
        /// 获取要处理的文件
        /// </summary>
        /// <returns></returns>
        private bool getFileList()
        {
            if (openIMGFile.ShowDialog() == DialogResult.OK)
            {
                fileList = openIMGFile.FileNames;
                if (fileList.Length > 0) return true;
                else return false;
            }
            else return false;
        }

        #region 修改EXIF
        private void startUP(string filePath, ref List<PropertyItem> ProItems, ref bool NeedWrite)
        {
            byte[] val;
            double[] newParmas = info_list[Path.GetFileName(filePath)];

            double newLongitude = newParmas[0];     //经
            double newLatitude = newParmas[1];      //纬
            double newAltitude = newParmas[2];      //高
            double newFouclen = newParmas.Length == 4 ? newParmas[3] : double.NaN;      //焦

            //再次取得所有的属性(以PropertyId做排序)  
            Bitmap bmp = new Bitmap(filePath);
            List<PropertyItem> propertyItems = bmp.PropertyItems.OrderBy(x => x.Id).ToList();

            //if (!ListItemIDEqual(propertyItems,0))//给重新赋GPS信息:EXIF版本
            //{
            val = new byte[4];
            propertyItems[0].Id = 0;
            propertyItems[0].Len = 4;
            propertyItems[0].Type = 1;
            val[0] = 2;
            val[1] = 3;
            val[2] = 0;
            val[3] = 0;
            propertyItems[0].Value = val;
            propertyItems.Add(propertyItems[0]);
            NeedWrite = true;
            //}
            //if (!ListItemIDEqual(propertyItems, 1))//给重新赋GPS信息
            //{
            propertyItems[1].Id = 1;
            propertyItems[1].Len = 2;
            propertyItems[1].Type = 2;
            val = new byte[2];
            val[0] = 78;
            val[1] = 0;
            propertyItems[1].Value = val;
            propertyItems.Add(propertyItems[1]);
            NeedWrite = true;
            //}
            //if (!ListItemIDEqual(propertyItems,2))//给重新赋GPS信息:设置纬度
            //{
            propertyItems[2].Id = 2;
            propertyItems[2].Len = 24;
            propertyItems[2].Type = 5;
            val = new byte[24];
            propertyItems[2].Value = setCoodr(newLatitude);// this.ReverseCood(newLatitude);
            propertyItems.Add(propertyItems[2]);
            NeedWrite = true;
            //}
            //if (!ListItemIDEqual(propertyItems, 3))//给重新赋GPS信息
            //{
            propertyItems[3].Id = 3;
            propertyItems[3].Len = 2;
            propertyItems[3].Type = 2;
            val = new byte[2];
            val[0] = 69;
            val[1] = 0;
            propertyItems[3].Value = val;
            propertyItems.Add(propertyItems[3]);
            NeedWrite = true;
            //}
            //if (!ListItemIDEqual(propertyItems, 4))//给重新赋GPS信息:设置经度
            //{
            propertyItems[4].Id = 4;
            propertyItems[4].Len = 24;
            propertyItems[4].Type = 5;
            val = new byte[24];
            propertyItems[4].Value = setCoodr(newLongitude);// this.ReverseCood(newLongitude);
            propertyItems.Add(propertyItems[4]);
            NeedWrite = true;
            //}
            //if (!ListItemIDEqual(propertyItems, 6))//给重新赋GPS信息:设置海拔高度
            //{
            propertyItems[6].Id = 6;
            propertyItems[6].Len = 24;
            propertyItems[6].Type = 5;
            val = new byte[8];
            propertyItems[6].Value = setAltitude(newAltitude);// this.ReverseAltitude(newAltitude);
            propertyItems.Add(propertyItems[6]);
            NeedWrite = true;
            //}
            //0x920A 焦距
            if (!double.IsNaN(newFouclen))
            {
                PropertyItem ddd = propertyItems.Find(x => x.Id == 37386);
                ddd.Value = setAltitude(newFouclen);
                propertyItems.Add(ddd);
            }
            ProItems = propertyItems;

            bmp.Dispose();
        }
        /// <summary>
        /// 修改坐标值
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private byte[] setCoodr(double num)
        {
            decimal cood = (decimal)Math.Abs(num);
            uint du = (uint)cood;
            uint fen = (uint)((cood - du) * 60);
            double miao = (double)((cood - du) * 60 - fen) * 60;

            //坐标是一个byte[24]数组
            List<byte> _byteList = new List<byte>();
            _byteList.AddRange(BitConverter.GetBytes(du));  //塞入度的分子
            _byteList.AddRange(BitConverter.GetBytes(1));   //塞入度的分母
            _byteList.AddRange(BitConverter.GetBytes(fen)); //塞入分的分子
            _byteList.AddRange(BitConverter.GetBytes(1));   //塞入分的分母
            uint fz, fm;
            getFenshu(miao, out fz, out fm);
            _byteList.AddRange(BitConverter.GetBytes(fz));  //塞入秒的分子
            _byteList.AddRange(BitConverter.GetBytes(fm));  //塞入秒的分子

            return _byteList.ToArray();
        }
        /// <summary>
        /// 设置海拔高度
        /// </summary>
        /// <param name="alt"></param>
        /// <returns></returns>
        private byte[] setAltitude(double alt)
        {
            //海拔是一个byte[8]数组
            List<byte> _byteList = new List<byte>();

            //把double转成 fz / fm
            uint fz, fm;
            getFenshu(alt, out fz, out fm);

            //fz => byte[4] , fm => byte[4]
            _byteList.AddRange(BitConverter.GetBytes(fz));
            _byteList.AddRange(BitConverter.GetBytes(fm));

            return _byteList.ToArray();
        }
        void getFenshu(double myNum, out uint fenzi, out uint fenmu)
        {
            //byte[]mm = BitConverter.GetBytes(m);
            //uint o = BitConverter.ToUInt32(mm,0);
            double a = Math.Abs(myNum);
            if (a > 999999999)
            {
                MessageBox.Show("整数部份数值过大！");
                fenzi = 0;
                fenmu = 1;
                return;
            }
            uint b = (uint)Math.Floor(a);                   //整数部份
            decimal c = (decimal)a - b;                     //小数部份
            if (c == 0)
            {
                fenzi = b;
                fenmu = 1;
                return;
            }

            uint e = 1;                                     //小数点后位数
            decimal d = c;

            int k = a.ToString().IndexOf(".");
            double de = Math.Pow(10, 9 - k);           //用整数部份的位数动态限制小数点后位数，总位数不超过9位
            while (d > 0 && e < de)
            {
                d *= 10;
                e *= 10;
                uint x = (uint)d;
                d = (decimal)(d - x);
            }
            uint f = Convert.ToUInt32(c * e);               //小数部份整数化
            if (e % f == 0)                                 //f可以被e整除时，直接获取分数，如：25/100
            {
                fenzi = 1;
                fenmu = e / f;
            }
            else                                //e/f不能整除时要遍历f，获取f和e的最大公约数
            {
                uint y = f - f / 2;              //最大公约数除本身外，不会大于本身的1/2
                for (; y > 1; y--)              //suCount不能为0，也不能为1，任何数和1求余都是0
                {
                    if ((f % y == 0) && (e % y == 0))           //从数值中间递减，能同时被f和e整除的数就是它们的公约数
                        break;
                }
                fenzi = y > 1 ? f / y : f;
                fenmu = y > 1 ? e / y : e;
            }
            fenzi = fenmu * b + fenzi;                 //3又2/5  ==>   2+3*5 / 5   ==>  17 / 5
        }
        #endregion

        #region 重写文件
        private static void WriteNewDescriptionInImage(string Filename, List<PropertyItem> myProItems)
        {
            Image Pic;
            //int i;
            string FilenameTemp;
            System.Drawing.Imaging.Encoder Enc = System.Drawing.Imaging.Encoder.Transformation;//编码器
            EncoderParameters EncParms = new EncoderParameters(1);
            EncoderParameter EncParm;
            //ImageCodecInfo CodecInfo = GetEncoderInfo("image/jpeg");
            ImageCodecInfo CodecInfo = GetEncoderInfoByExtension(Path.GetExtension(Filename));
            // load the image to change加载图像变化
            Pic = Image.FromFile(Filename);
            foreach (PropertyItem item in myProItems)
            {
                Pic.SetPropertyItem(item);
            }
            // we cannot store in the same image, so use a temporary image instead
            //我们不能存储在相同的图像，所以使用一个临时的图像代替
            FilenameTemp = Filename + ".temp";
            // for lossless rewriting must rotate the image by 90 degrees!无损重写必须图像旋转90度！
            EncParm = new EncoderParameter(Enc, (long)EncoderValue.TransformRotate90);
            EncParms.Param[0] = EncParm;
            // now write the rotated image with new description现在写的旋转图像的新描述
            Pic.Save(FilenameTemp, CodecInfo, EncParms);
            // for computers with low memory and large pictures: release memory now电脑内存和释放内存现在：大图片
            Pic.Dispose();
            Pic = null;
            GC.Collect();
            // delete the original file, will be replaced later删除原始文件，将被替换后
            System.IO.File.Delete(Filename);
            // now must rotate back the written picture现在必须轮流回写图像
            Pic = Image.FromFile(FilenameTemp);
            EncParm = new EncoderParameter(Enc, (long)EncoderValue.TransformRotate270);
            EncParms.Param[0] = EncParm;

            Pic.Save(Filename, CodecInfo, EncParms);
            // release memory now释放内存
            Pic.Dispose();
            Pic = null;
            GC.Collect();
            // delete the temporary picture删除临时图片
            System.IO.File.Delete(FilenameTemp);
        }
        private static ImageCodecInfo GetEncoderInfoByExtension(String extensionName)
        {
            int j;
            ImageCodecInfo[] encoders;
            extensionName = extensionName.ToUpper();
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].FilenameExtension.IndexOf(extensionName) >= 0)
                    return encoders[j];
            }
            return null;
        }
        /// <summary>
        /// 读EXIF
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private string getGPS(string filePath)
        {
            //打开文件同，取得所有的属性(以PropertyId做排序)  
            Bitmap bmp = new Bitmap(filePath);
            List<PropertyItem> propertyItems = bmp.PropertyItems.OrderBy(x => x.Id).ToList();
            bmp.Dispose();

            string report = "";

            foreach (PropertyItem objItem in propertyItems)
            {
                switch (objItem.Id)
                {
                    case 0x0000:
                        var query = from tmpb in objItem.Value select tmpb.ToString();
                        string sreVersion = string.Join(".", query.ToArray());
                        break;

                    case 0x0001:
                        chrGPSLatitudeRef = BitConverter.ToChar(objItem.Value, 0);
                        break;

                    case 0x0002:
                        if (objItem.Value.Length == 24 && objItem.Type == 5)
                        {
                            //degrees(将byte[0]~byte[3]转成uint, 除以byte[4]~byte[7]转成的uint) 
                            double d = BitConverter.ToUInt32(objItem.Value, 0) * 1.0d / BitConverter.ToUInt32(objItem.Value, 4);
                            //minutes(將byte[8]~byte[11]转成uint, 除以byte[12]~byte[15]转成的uint)   
                            double m = BitConverter.ToUInt32(objItem.Value, 8) * 1.0d / BitConverter.ToUInt32(objItem.Value, 12);
                            //seconds(將byte[16]~byte[19]转成uint, 除以byte[20]~byte[23]转成的uint)   
                            double s = BitConverter.ToUInt32(objItem.Value, 16) * 1.0d / BitConverter.ToUInt32(objItem.Value, 20);
                            //计算经纬度数值, 如果是南纬, 要乘上(-1)   
                            double dblGPSLatitude = (((s / 60 + m) / 60) + d) * (chrGPSLatitudeRef.Equals('N') ? 1 : -1);
                            string strLatitude = string.Format("{0:#} deg {1:#}' {2:#.00}\" {3}", d, m, s, chrGPSLatitudeRef);
                            //纬度+经度
                            report += dblGPSLatitude + "+";
                        }
                        break;

                    case 0x0003:
                        //透过BitConverter, 将Value转成Char('E' / 'W')   
                        //此值在后续的Longitude计算上会用到   
                        chrGPSLongitudeRef = BitConverter.ToChar(objItem.Value, 0);
                        break;

                    case 0x0004:
                        if (objItem.Value.Length == 24)
                        {
                            //degrees(将byte[0]~byte[3]转成uint, 除以byte[4]~byte[7]转成的uint)   
                            double d = BitConverter.ToUInt32(objItem.Value, 0) * 1.0d / BitConverter.ToUInt32(objItem.Value, 4);
                            //minutes(将byte[8]~byte[11]转成uint, 除以byte[12]~byte[15]转成的uint)   
                            double m = BitConverter.ToUInt32(objItem.Value, 8) * 1.0d / BitConverter.ToUInt32(objItem.Value, 12);
                            //seconds(将byte[16]~byte[19]转成uint, 除以byte[20]~byte[23]转成的uint)   
                            double s = BitConverter.ToUInt32(objItem.Value, 16) * 1.0d / BitConverter.ToUInt32(objItem.Value, 20);
                            //计算精度的数值, 如果是西经, 要乘上(-1)   
                            double dblGPSLongitude = (((s / 60 + m) / 60) + d) * (chrGPSLongitudeRef.Equals('E') ? 1 : -1);
                            report += dblGPSLongitude + "+";
                        }
                        break;

                    case 0x0005:
                        string strAltitude = BitConverter.ToBoolean(objItem.Value, 0) ? "0" : "1";
                        break;

                    case 0x0006:
                        if (objItem.Value.Length == 8)
                        {
                            //将byte[0]~byte[3]转成uint, 除以byte[4]~byte[7]转成的uint  
                            uint z = BitConverter.ToUInt32(objItem.Value, 0);
                            uint m = BitConverter.ToUInt32(objItem.Value, 4);
                            double dblAltitude = BitConverter.ToUInt32(objItem.Value, 0) * 1.0d / BitConverter.ToUInt32(objItem.Value, 4);
                            report += "*" + dblAltitude;
                        }
                        break;

                    case 0x920A:
                        int ff = objItem.Id;
                        byte[] val = objItem.Value;
                        Console.Write("");
                        break;
                }

            }
            return report;
        }
        #endregion

        /* IDlist
ID     | Property tag           |   
-------+------------------------+-----------------
0x0000 | GpsVer                 |   EXIF版本
0x0001 | GpsLatitudeRef
0x0002 | GpsLatitude
0x0003 | GpsLongitudeRef
0x0004 | GpsLongitude
0x0005 | GpsAltitudeRef
0x0006 | GpsAltitude
0x0007 | GpsGpsTime
0x0008 | GpsGpsSatellites
0x0009 | GpsGpsStatus
0x000A | GpsGpsMeasureMode
0x000B | GpsGpsDop
0x000C | GpsSpeedRef
0x000D | GpsSpeed
0x000E | GpsTrackRef
0x000F | GpsTrack
0x0010 | GpsImgDirRef
0x0011 | GpsImgDir
0x0012 | GpsMapDatum
0x0013 | GpsDestLatRef
0x0014 | GpsDestLat
0x0015 | GpsDestLongRef
0x0016 | GpsDestLong
0x0017 | GpsDestBearRef
0x0018 | GpsDestBear
0x0019 | GpsDestDistRef
0x001A | GpsDestDist
0x00FE | NewSubfileType
0x00FF | SubfileType
0x0100 | ImageWidth
0x0101 | ImageHeight
0x0102 | BitsPerSample
0x0103 | Compression
0x0106 | PhotometricInterp
0x0107 | ThreshHolding
0x0108 | CellWidth
0x0109 | CellHeight
0x010A | FillOrder
0x010D | DocumentName
0x010E | ImageDescription
0x010F | EquipMake
0x0110 | EquipModel
0x0111 | StripOffsets
0x0112 | Orientation
0x0115 | SamplesPerPixel
0x0116 | RowsPerStrip
0x0117 | StripBytesCount
0x0118 | MinSampleValue
0x0119 | MaxSampleValue
0x011A | XResolution
0x011B | YResolution
0x011C | PlanarConfig
0x011D | PageName
0x011E | XPosition
0x011F | YPosition
0x0120 | FreeOffset
0x0121 | FreeByteCounts
0x0122 | GrayResponseUnit
0x0123 | GrayResponseCurve
0x0124 | T4Option
0x0125 | T6Option
0x0128 | ResolutionUnit
0x0129 | PageNumber
0x012D | TransferFunction
0x0131 | SoftwareUsed
0x0132 | DateTime
0x013B | Artist
0x013C | HostComputer
0x013D | Predictor
0x013E | WhitePoint
0x013F | PrimaryChromaticities
0x0140 | ColorMap
0x0141 | HalftoneHints
0x0142 | TileWidth
0x0143 | TileLength
0x0144 | TileOffset
0x0145 | TileByteCounts
0x014C | InkSet
0x014D | InkNames
0x014E | NumberOfInks
0x0150 | DotRange
0x0151 | TargetPrinter
0x0152 | ExtraSamples
0x0153 | SampleFormat
0x0154 | SMinSampleValue
0x0155 | SMaxSampleValue
0x0156 | TransferRange
0x0200 | JPEGProc
0x0201 | JPEGInterFormat
0x0202 | JPEGInterLength
0x0203 | JPEGRestartInterval
0x0205 | JPEGLosslessPredictors
0x0206 | JPEGPointTransforms
0x0207 | JPEGQTables
0x0208 | JPEGDCTables
0x0209 | JPEGACTables
0x0211 | YCbCrCoefficients
0x0212 | YCbCrSubsampling
0x0213 | YCbCrPositioning
0x0214 | REFBlackWhite
0x0301 | Gamma
0x0302 | ICCProfileDescriptor
0x0303 | SRGBRenderingIntent
0x0320 | ImageTitle
0x5001 | ResolutionXUnit
0x5002 | ResolutionYUnit
0x5003 | ResolutionXLengthUnit
0x5004 | ResolutionYLengthUnit
0x5005 | PrintFlags
0x5006 | PrintFlagsVersion
0x5007 | PrintFlagsCrop
0x5008 | PrintFlagsBleedWidth
0x5009 | PrintFlagsBleedWidthScale
0x500A | HalftoneLPI
0x500B | HalftoneLPIUnit
0x500C | HalftoneDegree
0x500D | HalftoneShape
0x500E | HalftoneMisc
0x500F | HalftoneScreen
0x5010 | JPEGQuality
0x5011 | GridSize
0x5012 | ThumbnailFormat
0x5013 | ThumbnailWidth
0x5014 | ThumbnailHeight
0x5015 | ThumbnailColorDepth
0x5016 | ThumbnailPlanes
0x5017 | ThumbnailRawBytes
0x5018 | ThumbnailSize
0x5019 | ThumbnailCompressedSize
0x501A | ColorTransferFunction
0x501B | ThumbnailData
0x5020 | ThumbnailImageWidth
0x5021 | ThumbnailImageHeight
0x5022 | ThumbnailBitsPerSample
0x5023 | ThumbnailCompression
0x5024 | ThumbnailPhotometricInterp
0x5025 | ThumbnailImageDescription
0x5026 | ThumbnailEquipMake
0x5027 | ThumbnailEquipModel
0x5028 | ThumbnailStripOffsets
0x5029 | ThumbnailOrientation
0x502A | ThumbnailSamplesPerPixel
0x502B | ThumbnailRowsPerStrip
0x502C | ThumbnailStripBytesCount
0x502D | ThumbnailResolutionX
0x502E | ThumbnailResolutionY
0x502F | ThumbnailPlanarConfig
0x5030 | ThumbnailResolutionUnit
0x5031 | ThumbnailTransferFunction
0x5032 | ThumbnailSoftwareUsed
0x5033 | ThumbnailDateTime
0x5034 | ThumbnailArtist
0x5035 | ThumbnailWhitePoint
0x5036 | ThumbnailPrimaryChromaticities
0x5037 | ThumbnailYCbCrCoefficients
0x5038 | ThumbnailYCbCrSubsampling
0x5039 | ThumbnailYCbCrPositioning
0x503A | ThumbnailRefBlackWhite
0x503B | ThumbnailCopyRight
0x5090 | LuminanceTable
0x5091 | ChrominanceTable
0x5100 | FrameDelay
0x5101 | LoopCount
0x5102 | GlobalPalette
0x5103 | IndexBackground
0x5104 | IndexTransparent
0x5110 | PixelUnit
0x5111 | PixelPerUnitX
0x5112 | PixelPerUnitY
0x5113 | PaletteHistogram
0x8298 | Copyright
0x829A | ExifExposureTime
0x829D | ExifFNumber
0x8769 | ExifIFD
0x8773 | ICCProfile
0x8822 | ExifExposureProg
0x8824 | ExifSpectralSense
0x8825 | GpsIFD
0x8827 | ExifISOSpeed
0x8828 | ExifOECF
0x9000 | ExifVer
0x9003 | ExifDTOrig
0x9004 | ExifDTDigitized
0x9101 | ExifCompConfig
0x9102 | ExifCompBPP
0x9201 | ExifShutterSpeed
0x9202 | ExifAperture
0x9203 | ExifBrightness
0x9204 | ExifExposureBias
0x9205 | ExifMaxAperture
0x9206 | ExifSubjectDist
0x9207 | ExifMeteringMode
0x9208 | ExifLightSource
0x9209 | ExifFlash
0x920A | ExifFocalLength
0x927C | ExifMakerNote
0x9286 | ExifUserComment
0x9290 | ExifDTSubsec
0x9291 | ExifDTOrigSS
0x9292 | ExifDTDigSS
0xA000 | ExifFPXVer
0xA001 | ExifColorSpace
0xA002 | ExifPixXDim
0xA003 | ExifPixYDim
0xA004 | ExifRelatedWav
0xA005 | ExifInterop
0xA20B | ExifFlashEnergy
0xA20C | ExifSpatialFR
0xA20E | ExifFocalXRes
0xA20F | ExifFocalYRes
0xA210 | ExifFocalResUnit
0xA214 | ExifSubjectLoc
0xA215 | ExifExposureIndex
0xA217 | ExifSensingMethod
0xA300 | ExifFileSource
0xA301 | ExifSceneType
0xA302 | ExifCfaPattern
             */
    }
}

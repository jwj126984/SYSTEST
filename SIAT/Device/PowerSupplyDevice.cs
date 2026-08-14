using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIAT.ResourceManagement;
using SIAT.TSET;

namespace SIAT.Devices
{
    /// <summary>
    /// 电源设备 - 通讯协议写死在各步骤方法中，通过设置的通讯方式（串口）发送
    /// </summary>
    [DeviceDefinition("电源设备", Description = "可编程电源", CommunicationType = CommunicationType.Serial)]
    public class PowerSupplyDevice : DeviceBase
    {
        /// <summary>
        /// 电流标定步骤
        /// 协议: 帧头 0xFF 0xFA + 命令码 0x03 + K值(4字节) + B值(4字节) + CRC8 + 帧尾 0x0A 0x0B
        /// </summary>
        [StepDefinition("电流标定", Description = "对设备进行电流标定", StepType = StepType.SendAndReceive)]
        [InputBinding("V10", "电源电压10V")]
        [InputBinding("v10", "产品电压10V")]
        [InputBinding("V12", "电源电压12V")]
        [InputBinding("v12", "产品电压12V")]
        [InputBinding("V14", "电源电压14V")]
        [InputBinding("v14", "产品电压14V")]
        [OutputBinding("READK", "读取K值")]
        [OutputBinding("READB", "读取B值")]
        public async Task<TestStepResult> 电流标定Step()
        {
            if (!IsDeviceConnected())
            {
                return new TestStepResult
                {
                    StepName = "电流标定",
                    IsSuccess = false,
                    ErrorMessage = "设备未连接",
                    ActualValue = "执行失败: 设备未连接"
                };
            }

            try
            {
                // 获取输入值
                string V10 = GetInputValue("V10", "0");
                string v10 = GetInputValue("v10", "0");
                string V12 = GetInputValue("V12", "0");
                string v12 = GetInputValue("v12", "0");
                string V14 = GetInputValue("V14", "0");
                string v14 = GetInputValue("v14", "0");

                // 计算标定参数 K 和 B（线性拟合 y = Kx + B）
                var points = new List<(double x, double y)>
                {
                    (double.Parse(v10), double.Parse(V10)),
                    (double.Parse(v12), double.Parse(V12)),
                    (double.Parse(v14), double.Parse(V14))
                };
                var (k, b) = LinearFit(points);

                // 构建发送协议 - 写入K和B到设备
                List<byte> data = new() { 0xFF, 0xFA, 0x00, 0x00, 0x03 };
                byte[] bytesK = BitConverter.GetBytes((float)k);
                Array.Reverse(bytesK);
                data.AddRange(bytesK);
                byte[] bytesB = BitConverter.GetBytes((float)b);
                Array.Reverse(bytesB);
                data.AddRange(bytesB);
                data.Add(PluginStepExecutor.CRC8Calculator.CalculateCRC8(data));
                data.Add(0x0A);
                data.Add(0x0B);

                // 通过设备通讯发送数据
                await SendDataAsync( data.ToArray());
                byte[] result1 = await ReceiveDataAsync(true);

                // 构建读取K和B的协议
                List<byte> data1 = new() { 0xFF, 0xFA, 0x00, 0x00, 0x04 };
                data1.Add(PluginStepExecutor.CRC8Calculator.CalculateCRC8(data1));
                data1.Add(0x0A);
                data1.Add(0x0B);

                await SendDataAsync( data1.ToArray());
                byte[] result = await ReceiveDataAsync(true);

                // 解析返回的K和B值
                double readK = BitConverter.ToSingle(new byte[] { result[6], result[7], result[8], result[9] }, 0);
                double readB = BitConverter.ToSingle(new byte[] { result[10], result[11], result[12], result[13] }, 0);

                SetOutputValue("READK", readK);
                SetOutputValue("READB", readB);

                return new TestStepResult
                {
                    StepName = "电流标定",
                    IsSuccess = true,
                    ActualValue = $"K={readK:F4}, B={readB:F4}"
                };
            }
            catch (Exception ex)
            {
                return new TestStepResult
                {
                    StepName = "电流标定",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ActualValue = "执行失败"
                };
            }
        }

        /// <summary>
        /// 读取电压步骤
        /// 协议: 帧头 0xFF 0xFA + 命令码 0x01 + CRC8 + 帧尾 0x0A 0x0B
        /// </summary>
        [StepDefinition("读取电压", Description = "读取设备当前电压", StepType = StepType.ReadOnly)]
        [OutputBinding("Volt", "读取的电压值")]
        public async Task<TestStepResult> 读取电压Step()
        {
            if (!IsDeviceConnected())
            {
                return new TestStepResult
                {
                    StepName = "读取电压",
                    IsSuccess = false,
                    ErrorMessage = "设备未连接",
                    ActualValue = "执行失败: 设备未连接"
                };
            }

            try
            {
                // 构建读取电压协议
                List<byte> data = new() { 0xFF, 0xFA, 0x00, 0x00, 0x01 };
                data.Add(PluginStepExecutor.CRC8Calculator.CalculateCRC8(data));
                data.Add(0x0A);
                data.Add(0x0B);

                // 通过设备通讯发送并接收
                await SendDataAsync( data.ToArray());
                byte[] result = await ReceiveDataAsync(true);

                // 解析电压值（4字节浮点数，从第6字节开始）
                double volt = BitConverter.ToSingle(new byte[] { result[6], result[7], result[8], result[9] }, 0);

                SetOutputValue("Volt", volt);

                return new TestStepResult
                {
                    StepName = "读取电压",
                    IsSuccess = true,
                    ActualValue = $"{volt:F3}V"
                };
            }
            catch (Exception ex)
            {
                return new TestStepResult
                {
                    StepName = "读取电压",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ActualValue = "执行失败"
                };
            }
        }

        /// <summary>
        /// 线性拟合计算 K 和 B (y = Kx + B)
        /// </summary>
        private static (double K, double B) LinearFit(List<(double x, double y)> points)
        {
            int n = points.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            foreach (var (x, y) in points)
            {
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }
            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
                return (0, sumY / n);
            double k = (n * sumXY - sumX * sumY) / denominator;
            double b = (sumY - k * sumX) / n;
            return (k, b);
        }
    }
}

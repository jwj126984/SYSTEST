using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using OfficeOpenXml;

namespace SIAT.TSET
{
    /// <summary>
    /// 测试报告生成器 - 生成详细的测试执行报告
    /// </summary>
    public class TestReportGenerator
    {
        /// <summary>
        /// 生成详细测试报告
        /// </summary>
        public static string GenerateDetailedReport(TestCaseConfig testCase, List<TestStepResult> stepResults, 
            string barcode, TimeSpan totalDuration, bool testPassed)
        {
            var report = new StringBuilder();
            
            // 报告头部
            report.AppendLine("========================================");
            report.AppendLine("           测试执行报告");
            report.AppendLine("========================================");
            report.AppendLine();
            
            // 测试基本信息
            report.AppendLine("测试基本信息:");
            report.AppendLine($"  测试用例: {testCase.Name}");
            report.AppendLine($"  产品条码: {barcode}");
            report.AppendLine($"  执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"  总耗时: {totalDuration.TotalSeconds:F3} 秒");
            report.AppendLine($"  测试结果: {(testPassed ? "通过" : "失败")}");
            report.AppendLine();
            
            // 项目统计
            int totalSteps = stepResults.Count;
            int passedSteps = stepResults.Count(r => r.IsSuccess);
            int failedSteps = stepResults.Count(r => !r.IsSuccess);
            double passRate = totalSteps > 0 ? (double)passedSteps / totalSteps * 100 : 0;
            
            report.AppendLine("测试统计:");
            report.AppendLine($"  总步骤数: {totalSteps}");
            report.AppendLine($"  通过步骤: {passedSteps}");
            report.AppendLine($"  失败步骤: {failedSteps}");
            report.AppendLine($"  通过率: {passRate:F2}%");
            report.AppendLine();
            
            // 详细步骤结果
            report.AppendLine("详细步骤执行结果:");
            report.AppendLine("========================================");
            
            for (int i = 0; i < stepResults.Count; i++)
            {
                var result = stepResults[i];
                report.AppendLine($"步骤 {i + 1}: {result.StepName}");
                report.AppendLine($"  状态: {(result.IsSuccess ? "✓ 通过" : "✗ 失败")}");
                report.AppendLine($"  耗时: {result.Duration.TotalSeconds:F3} 秒");
                
                if (!string.IsNullOrEmpty(result.ActualValue))
                {
                    report.AppendLine($"  实际值: {result.ActualValue}");
                }
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    report.AppendLine($"  错误信息: {result.ErrorMessage}");
                }
                
                report.AppendLine();
            }
            
            // 失败步骤汇总
            var failedResults = stepResults.Where(r => !r.IsSuccess).ToList();
            if (failedResults.Any())
            {
                report.AppendLine("失败步骤汇总:");
                report.AppendLine("========================================");
                
                foreach (var failedResult in failedResults)
                {
                    report.AppendLine($"• {failedResult.StepName}");
                    if (!string.IsNullOrEmpty(failedResult.ErrorMessage))
                    {
                        report.AppendLine($"  错误: {failedResult.ErrorMessage}");
                    }
                }
                report.AppendLine();
            }
            
            // 测试结论
            report.AppendLine("测试结论:");
            report.AppendLine("========================================");
            if (testPassed)
            {
                report.AppendLine("✓ 测试通过 - 所有测试步骤均执行成功");
            }
            else
            {
                report.AppendLine("✗ 测试失败 - 存在失败的测试步骤");
                report.AppendLine($"  失败步骤数量: {failedSteps}");
            }
            
            report.AppendLine();
            report.AppendLine("报告生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            return report.ToString();
        }
        
        /// <summary>
        /// 生成HTML格式的测试报告
        /// </summary>
        public static string GenerateHtmlReport(TestCaseConfig testCase, List<TestStepResult> stepResults, 
            string barcode, TimeSpan totalDuration, bool testPassed)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"zh-CN\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <title>测试执行报告</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("        .header { background: #f5f5f5; padding: 20px; border-radius: 5px; margin-bottom: 20px; }");
            html.AppendLine("        .summary { background: #e8f4f8; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
            html.AppendLine("        .step-result { margin: 10px 0; padding: 10px; border-left: 4px solid #4CAF50; background: #f9f9f9; }");
            html.AppendLine("        .step-failed { border-left-color: #f44336; background: #ffeaea; }");
            html.AppendLine("        .conclusion { padding: 15px; border-radius: 5px; margin-top: 20px; }");
            html.AppendLine("        .passed { background: #e8f5e8; border: 1px solid #4CAF50; }");
            html.AppendLine("        .failed { background: #ffeaea; border: 1px solid #f44336; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // 报告头部
            html.AppendLine("    <div class=\"header\">");
            html.AppendLine("        <h1>测试执行报告</h1>");
            html.AppendLine("    </div>");
            
            // 测试基本信息
            html.AppendLine("    <div class=\"summary\">");
            html.AppendLine("        <h2>测试基本信息</h2>");
            html.AppendLine($"        <p><strong>测试用例:</strong> {testCase.Name}</p>");
            html.AppendLine($"        <p><strong>产品条码:</strong> {barcode}</p>");
            html.AppendLine($"        <p><strong>执行时间:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"        <p><strong>总耗时:</strong> {totalDuration.TotalSeconds:F3} 秒</p>");
            html.AppendLine($"        <p><strong>测试结果:</strong> <span style=\"color: {(testPassed ? "#4CAF50" : "#f44336")}\">{(testPassed ? "通过" : "失败")}</span></p>");
            html.AppendLine("    </div>");
            
            // 测试统计
            int totalSteps = stepResults.Count;
            int passedSteps = stepResults.Count(r => r.IsSuccess);
            int failedSteps = stepResults.Count(r => !r.IsSuccess);
            double passRate = totalSteps > 0 ? (double)passedSteps / totalSteps * 100 : 0;
            
            html.AppendLine("    <div class=\"summary\">");
            html.AppendLine("        <h2>测试统计</h2>");
            html.AppendLine($"        <p><strong>总步骤数:</strong> {totalSteps}</p>");
            html.AppendLine($"        <p><strong>通过步骤:</strong> <span style=\"color: #4CAF50\">{passedSteps}</span></p>");
            html.AppendLine($"        <p><strong>失败步骤:</strong> <span style=\"color: #f44336\">{failedSteps}</span></p>");
            html.AppendLine($"        <p><strong>通过率:</strong> {passRate:F2}%</p>");
            html.AppendLine("    </div>");
            
            // 详细步骤结果
            html.AppendLine("    <h2>详细步骤执行结果</h2>");
            for (int i = 0; i < stepResults.Count; i++)
            {
                var result = stepResults[i];
                string stepClass = result.IsSuccess ? "step-result" : "step-result step-failed";
                
                html.AppendLine($"    <div class=\"{stepClass}\">");
                html.AppendLine($"        <h3>步骤 {i + 1}: {result.StepName}</h3>");
                html.AppendLine($"        <p><strong>状态:</strong> <span style=\"color: {(result.IsSuccess ? "#4CAF50" : "#f44336")}\">{(result.IsSuccess ? "✓ 通过" : "✗ 失败")}</span></p>");
                html.AppendLine($"        <p><strong>耗时:</strong> {result.Duration.TotalSeconds:F3} 秒</p>");
                
                if (!string.IsNullOrEmpty(result.ActualValue))
                {
                    html.AppendLine($"        <p><strong>实际值:</strong> {result.ActualValue}</p>");
                }
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    html.AppendLine($"        <p><strong>错误信息:</strong> {result.ErrorMessage}</p>");
                }
                
                html.AppendLine("    </div>");
            }
            
            // 测试结论
            string conclusionClass = testPassed ? "conclusion passed" : "conclusion failed";
            html.AppendLine($"    <div class=\"{conclusionClass}\">");
            html.AppendLine("        <h2>测试结论</h2>");
            if (testPassed)
            {
                html.AppendLine("        <p>✓ 测试通过 - 所有测试步骤均执行成功</p>");
            }
            else
            {
                html.AppendLine("        <p>✗ 测试失败 - 存在失败的测试步骤</p>");
                html.AppendLine($"        <p><strong>失败步骤数量:</strong> {failedSteps}</p>");
            }
            html.AppendLine("    </div>");
            
            html.AppendLine($"    <p style=\"text-align: center; margin-top: 30px; color: #666;\">报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        /// <summary>
        /// 生成Excel格式的测试报告
        /// </summary>
        public static byte[] GenerateExcelReport(TestCaseConfig testCase, List<TestStepResult> stepResults, 
            string barcode, TimeSpan totalDuration, bool testPassed)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("测试报告");
                
                int row = 1;
                
                // 报告标题
                worksheet.Cells[row, 1, row, 4].Merge = true;
                worksheet.Cells[row, 1].Value = "测试执行报告";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 16;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                row++;
                
                row++;
                
                // 测试基本信息
                worksheet.Cells[row, 1].Value = "测试基本信息";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 12;
                row++;
                
                worksheet.Cells[row, 1].Value = "测试用例:";
                worksheet.Cells[row, 2].Value = testCase.Name;
                row++;
                
                worksheet.Cells[row, 1].Value = "产品条码:";
                worksheet.Cells[row, 2].Value = barcode;
                row++;
                
                worksheet.Cells[row, 1].Value = "执行时间:";
                worksheet.Cells[row, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                row++;
                
                worksheet.Cells[row, 1].Value = "总耗时:";
                worksheet.Cells[row, 2].Value = $"{totalDuration.TotalSeconds:F3} 秒";
                row++;
                
                worksheet.Cells[row, 1].Value = "测试结果:";
                worksheet.Cells[row, 2].Value = testPassed ? "通过" : "失败";
                worksheet.Cells[row, 2].Style.Font.Color.SetColor(testPassed ? System.Drawing.Color.Green : System.Drawing.Color.Red);
                row++;
                
                row++;
                
                // 测试统计
                worksheet.Cells[row, 1].Value = "测试统计";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 12;
                row++;
                
                int totalSteps = stepResults.Count;
                int passedSteps = stepResults.Count(r => r.IsSuccess);
                int failedSteps = stepResults.Count(r => !r.IsSuccess);
                double passRate = totalSteps > 0 ? (double)passedSteps / totalSteps * 100 : 0;
                
                worksheet.Cells[row, 1].Value = "总步骤数:";
                worksheet.Cells[row, 2].Value = totalSteps;
                row++;
                
                worksheet.Cells[row, 1].Value = "通过步骤:";
                worksheet.Cells[row, 2].Value = passedSteps;
                worksheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                row++;
                
                worksheet.Cells[row, 1].Value = "失败步骤:";
                worksheet.Cells[row, 2].Value = failedSteps;
                worksheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                row++;
                
                worksheet.Cells[row, 1].Value = "通过率:";
                worksheet.Cells[row, 2].Value = $"{passRate:F2}%";
                row++;
                
                row++;
                
                // 详细步骤结果标题
                worksheet.Cells[row, 1].Value = "详细步骤执行结果";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 12;
                row++;
                
                // 表头
                row++;
                worksheet.Cells[row, 1].Value = "步骤序号";
                worksheet.Cells[row, 2].Value = "步骤名称";
                worksheet.Cells[row, 3].Value = "状态";
                worksheet.Cells[row, 4].Value = "耗时(秒)";
                worksheet.Cells[row, 5].Value = "实际值";
                worksheet.Cells[row, 6].Value = "错误信息";
                
                worksheet.Cells[row, 1, row, 6].Style.Font.Bold = true;
                worksheet.Cells[row, 1, row, 6].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[row, 1, row, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                worksheet.Cells[row, 1, row, 6].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                row++;
                
                // 步骤数据
                for (int i = 0; i < stepResults.Count; i++)
                {
                    var result = stepResults[i];
                    
                    worksheet.Cells[row, 1].Value = i + 1;
                    worksheet.Cells[row, 2].Value = result.StepName;
                    worksheet.Cells[row, 3].Value = result.IsSuccess ? "通过" : "失败";
                    worksheet.Cells[row, 3].Style.Font.Color.SetColor(result.IsSuccess ? System.Drawing.Color.Green : System.Drawing.Color.Red);
                    worksheet.Cells[row, 4].Value = result.Duration.TotalSeconds.ToString("F3");
                    worksheet.Cells[row, 5].Value = result.ActualValue;
                    worksheet.Cells[row, 6].Value = result.ErrorMessage;
                    
                    if (!result.IsSuccess)
                    {
                        worksheet.Cells[row, 1, row, 6].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                    }
                    
                    row++;
                }
                
                row++;
                
                // 测试结论
                worksheet.Cells[row, 1].Value = "测试结论";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 12;
                row++;
                
                row++;
                worksheet.Cells[row, 1, row, 4].Merge = true;
                if (testPassed)
                {
                    worksheet.Cells[row, 1].Value = "✓ 测试通过 - 所有测试步骤均执行成功";
                    worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                }
                else
                {
                    worksheet.Cells[row, 1].Value = $"✗ 测试失败 - 存在失败的测试步骤 (失败步骤数量: {failedSteps})";
                    worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                }
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                row++;
                
                row++;
                worksheet.Cells[row, 1].Value = "报告生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[row, 1].Style.Font.Italic = true;
                worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                
                // 调整列宽
                worksheet.Columns[1].Width = 12;
                worksheet.Columns[2].Width = 30;
                worksheet.Columns[3].Width = 10;
                worksheet.Columns[4].Width = 15;
                worksheet.Columns[5].Width = 25;
                worksheet.Columns[6].Width = 40;
                
                return package.GetAsByteArray();
            }
        }
        
        /// <summary>
        /// 保存报告到文件
        /// </summary>
        public static void SaveReportToFile(string reportContent, string barcode, string reportType = "txt")
        {
            try
            {
                // 创建报告目录
                string reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestReports");
                if (!Directory.Exists(reportDir))
                {
                    Directory.CreateDirectory(reportDir);
                }
                
                // 生成文件名
                string extension = reportType.ToLower() == "html" ? ".html" : ".txt";
                string fileName = $"{barcode}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                string filePath = Path.Combine(reportDir, fileName);
                
                // 写入报告文件
                File.WriteAllText(filePath, reportContent, Encoding.UTF8);
                
                // 记录保存成功
                System.Diagnostics.Debug.WriteLine($"测试报告已保存: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存测试报告失败: {ex.Message}");
            }
        }
    
    /// <summary>
    /// 保存Excel报告到文件
    /// </summary>
    public static void SaveExcelReportToFile(byte[] excelContent, string barcode)
    {
        try
        {
            string reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestReports");
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }
            
            string fileName = $"{barcode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string filePath = Path.Combine(reportDir, fileName);
            
            File.WriteAllBytes(filePath, excelContent);
            
            System.Diagnostics.Debug.WriteLine($"Excel测试报告已保存: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存Excel测试报告失败: {ex.Message}");
        }
    }
}
}
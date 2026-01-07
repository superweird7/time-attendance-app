using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ZKTecoManager
{
    class GenerateDocPdf
    {
        static void Main(string[] args)
        {
            string outputPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "دليل_برنامج_البصمة.pdf"
            );

            try
            {
                CreateArabicPdf(outputPath);
                Console.WriteLine("تم إنشاء الملف بنجاح: " + outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("خطأ: " + ex.Message);
            }
        }

        static void CreateArabicPdf(string outputPath)
        {
            Document doc = new Document(PageSize.A4, 50, 50, 50, 50);
            PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(outputPath, FileMode.Create));
            doc.Open();

            // Use Arial Unicode for Arabic support
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

            Font titleFont = new Font(bf, 28, Font.BOLD, new BaseColor(102, 126, 234));
            Font headerFont = new Font(bf, 18, Font.BOLD, new BaseColor(31, 41, 55));
            Font subHeaderFont = new Font(bf, 14, Font.BOLD, new BaseColor(79, 70, 229));
            Font normalFont = new Font(bf, 12, Font.NORMAL, new BaseColor(55, 65, 81));
            Font bulletFont = new Font(bf, 11, Font.NORMAL, new BaseColor(75, 85, 99));

            // Title
            Paragraph title = new Paragraph("دليل استخدام برنامج البصمة", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 10;
            doc.Add(title);

            Paragraph subtitle = new Paragraph("ZKTeco Manager - نظام إدارة الحضور والانصراف", subHeaderFont);
            subtitle.Alignment = Element.ALIGN_CENTER;
            subtitle.SpacingAfter = 30;
            doc.Add(subtitle);

            // Introduction
            AddSection(doc, "نظرة عامة على البرنامج", headerFont);
            AddParagraph(doc, @"برنامج البصمة (ZKTeco Manager) هو تطبيق سطح مكتب لإدارة أجهزة البصمة من نوع ZKTeco. يوفر البرنامج واجهة سهلة الاستخدام لإدارة الموظفين والأقسام والورديات وتتبع الحضور والانصراف.", normalFont);

            // Login Section
            AddSection(doc, "١. تسجيل الدخول", headerFont);
            AddSubSection(doc, "أنواع المستخدمين:", subHeaderFont);
            AddBullet(doc, "• مدير النظام (superadmin): صلاحيات كاملة على جميع الوظائف", bulletFont);
            AddBullet(doc, "• مدير القسم (deptadmin): صلاحيات محدودة على الأقسام والأجهزة المخصصة له", bulletFont);
            AddParagraph(doc, "لتسجيل الدخول: أدخل رقم البادج وكلمة المرور ثم اضغط 'دخول'", normalFont);

            // Main Dashboard
            AddSection(doc, "٢. لوحة التحكم الرئيسية", headerFont);
            AddParagraph(doc, "تعرض الشاشة الرئيسية جميع الوظائف المتاحة على شكل بطاقات:", normalFont);

            AddSubSection(doc, "Dashboard - لوحة المتابعة:", subHeaderFont);
            AddBullet(doc, "• عرض إحصائيات الحضور اليومية في الوقت الفعلي", bulletFont);
            AddBullet(doc, "• إجمالي الموظفين، الحاضرين، الغائبين، المتأخرين", bulletFont);
            AddBullet(doc, "• نسبة الحضور اليومية", bulletFont);
            AddBullet(doc, "• قائمة الغائبين والمتأخرين", bulletFont);
            AddBullet(doc, "• تحديث تلقائي كل 5 دقائق", bulletFont);

            AddSubSection(doc, "Employees - الموظفين:", subHeaderFont);
            AddBullet(doc, "• عرض قائمة جميع الموظفين مع البحث والفلترة", bulletFont);
            AddBullet(doc, "• إضافة موظف جديد (Ctrl+N)", bulletFont);
            AddBullet(doc, "• تعديل بيانات موظف (Ctrl+E)", bulletFont);
            AddBullet(doc, "• حذف موظف (Delete)", bulletFont);
            AddBullet(doc, "• إضافة مجموعة موظفين دفعة واحدة", bulletFont);

            AddSubSection(doc, "Departments - الأقسام:", subHeaderFont);
            AddBullet(doc, "• إدارة الهيكل التنظيمي للشركة", bulletFont);
            AddBullet(doc, "• إضافة وتعديل وحذف الأقسام", bulletFont);

            AddSubSection(doc, "Shifts - الورديات:", subHeaderFont);
            AddBullet(doc, "• تحديد أوقات العمل لكل وردية", bulletFont);
            AddBullet(doc, "• وقت الدخول والخروج المحدد", bulletFont);
            AddBullet(doc, "• تعيين الورديات للموظفين", bulletFont);

            // New Page
            doc.NewPage();

            AddSubSection(doc, "Devices - الأجهزة:", subHeaderFont);
            AddBullet(doc, "• إضافة وإدارة أجهزة البصمة ZKTeco", bulletFont);
            AddBullet(doc, "• الاتصال بالأجهزة عبر IP", bulletFont);
            AddBullet(doc, "• عرض حالة الاتصال", bulletFont);

            AddSubSection(doc, "Reports - التقارير:", subHeaderFont);
            AddBullet(doc, "• إنشاء تقارير الحضور والانصراف", bulletFont);
            AddBullet(doc, "• تصدير التقارير كملفات PDF", bulletFont);
            AddBullet(doc, "• فلترة حسب التاريخ والقسم والموظف", bulletFont);
            AddBullet(doc, "• عرض أوقات الدخول والخروج الفعلية", bulletFont);
            AddBullet(doc, "• حساب التأخير والغياب تلقائياً", bulletFont);
            AddBullet(doc, "• تعديل الأوقات يدوياً (للمستخدمين المصرح لهم)", bulletFont);

            AddSubSection(doc, "Exceptions - الاستثناءات:", subHeaderFont);
            AddBullet(doc, "• إدارة أنواع الاستثناءات (إجازة، مرضي، مهمة، إلخ)", bulletFont);
            AddBullet(doc, "• تسجيل استثناءات للموظفين", bulletFont);
            AddBullet(doc, "• تعيين استثناءات جماعية", bulletFont);

            AddSubSection(doc, "Backup - النسخ الاحتياطي:", subHeaderFont);
            AddBullet(doc, "• إنشاء نسخة احتياطية من قاعدة البيانات", bulletFont);
            AddBullet(doc, "• استعادة البيانات من نسخة احتياطية", bulletFont);
            AddBullet(doc, "• نسخ احتياطي تلقائي يومي", bulletFont);
            AddBullet(doc, "• التحقق من صحة النسخة الاحتياطية", bulletFont);

            AddSubSection(doc, "Data Sync - مزامنة البيانات:", subHeaderFont);
            AddBullet(doc, "• مزامنة البيانات بين الأجهزة والكمبيوتر", bulletFont);
            AddBullet(doc, "• سحب سجلات الحضور من الجهاز", bulletFont);
            AddBullet(doc, "• رفع بيانات الموظفين للجهاز", bulletFont);

            // Sync Operations
            AddSection(doc, "٣. عمليات المزامنة مع الأجهزة", headerFont);

            AddSubSection(doc, "من الجهاز إلى الكمبيوتر:", subHeaderFont);
            AddBullet(doc, "• سحب سجلات البصمة من جهاز ZKTeco", bulletFont);
            AddBullet(doc, "• استيراد الموظفين المسجلين على الجهاز", bulletFont);
            AddBullet(doc, "• تحديث قاعدة البيانات تلقائياً", bulletFont);

            AddSubSection(doc, "من الكمبيوتر إلى الجهاز:", subHeaderFont);
            AddBullet(doc, "• رفع بيانات الموظفين للجهاز", bulletFont);
            AddBullet(doc, "• مزامنة البصمات والأسماء", bulletFont);

            // New Page
            doc.NewPage();

            // Keyboard Shortcuts
            AddSection(doc, "٤. اختصارات لوحة المفاتيح", headerFont);

            AddSubSection(doc, "الشاشة الرئيسية:", subHeaderFont);
            AddBullet(doc, "• F1 - فتح لوحة المتابعة (Dashboard)", bulletFont);
            AddBullet(doc, "• F2 - فتح الموظفين", bulletFont);
            AddBullet(doc, "• F3 - فتح الأقسام", bulletFont);
            AddBullet(doc, "• F4 - فتح الورديات", bulletFont);
            AddBullet(doc, "• F5 - فتح الأجهزة", bulletFont);
            AddBullet(doc, "• F6 - فتح التقارير", bulletFont);
            AddBullet(doc, "• F7 - فتح مزامنة البيانات", bulletFont);
            AddBullet(doc, "• F8 - سجل التغييرات", bulletFont);
            AddBullet(doc, "• F9 - صحة النظام", bulletFont);
            AddBullet(doc, "• F10 - عرض اختصارات لوحة المفاتيح", bulletFont);
            AddBullet(doc, "• Escape - الخروج من البرنامج", bulletFont);

            AddSubSection(doc, "نوافذ البيانات:", subHeaderFont);
            AddBullet(doc, "• Ctrl+N - إضافة عنصر جديد", bulletFont);
            AddBullet(doc, "• Ctrl+E - تعديل العنصر المحدد", bulletFont);
            AddBullet(doc, "• Ctrl+F - البحث", bulletFont);
            AddBullet(doc, "• Delete - حذف العنصر المحدد", bulletFont);
            AddBullet(doc, "• Escape - إغلاق النافذة", bulletFont);

            // User Roles
            AddSection(doc, "٥. صلاحيات المستخدمين", headerFont);

            AddSubSection(doc, "مدير النظام (Superadmin):", subHeaderFont);
            AddBullet(doc, "• الوصول الكامل لجميع الأقسام والموظفين", bulletFont);
            AddBullet(doc, "• إدارة المستخدمين والصلاحيات", bulletFont);
            AddBullet(doc, "• لوحة الإدارة (Admin Panel)", bulletFont);
            AddBullet(doc, "• جميع عمليات النسخ الاحتياطي", bulletFont);
            AddBullet(doc, "• تعديل أوقات الحضور يدوياً", bulletFont);

            AddSubSection(doc, "مدير القسم (Deptadmin):", subHeaderFont);
            AddBullet(doc, "• الوصول للأقسام المخصصة له فقط", bulletFont);
            AddBullet(doc, "• الوصول للأجهزة المخصصة له فقط", bulletFont);
            AddBullet(doc, "• عرض تقارير القسم الخاص به", bulletFont);

            // Technical Info
            AddSection(doc, "٦. المتطلبات التقنية", headerFont);
            AddBullet(doc, "• نظام التشغيل: Windows 7 أو أحدث", bulletFont);
            AddBullet(doc, "• .NET Framework 4.7.2", bulletFont);
            AddBullet(doc, "• قاعدة البيانات: PostgreSQL", bulletFont);
            AddBullet(doc, "• أجهزة ZKTeco متوافقة", bulletFont);

            // New Page
            doc.NewPage();

            // Tips Section
            AddSection(doc, "٧. نصائح وإرشادات", headerFont);
            AddBullet(doc, "• قم بعمل نسخة احتياطية بشكل دوري", bulletFont);
            AddBullet(doc, "• تأكد من اتصال الأجهزة بالشبكة قبل المزامنة", bulletFont);
            AddBullet(doc, "• راجع تقارير الحضور يومياً لمتابعة الغياب والتأخير", bulletFont);
            AddBullet(doc, "• استخدم الاستثناءات لتسجيل الإجازات والمهمات", bulletFont);
            AddBullet(doc, "• حافظ على تحديث بيانات الموظفين", bulletFont);

            // Footer
            AddSection(doc, "٨. الدعم الفني", headerFont);
            AddParagraph(doc, "للمساعدة أو الاستفسارات، يرجى التواصل مع مسؤول النظام.", normalFont);

            Paragraph footer = new Paragraph("\n\n© ZKTeco Manager - جميع الحقوق محفوظة", normalFont);
            footer.Alignment = Element.ALIGN_CENTER;
            doc.Add(footer);

            doc.Close();
        }

        static void AddSection(Document doc, string text, Font font)
        {
            Paragraph p = new Paragraph(text, font);
            p.Alignment = Element.ALIGN_RIGHT;
            p.SpacingBefore = 20;
            p.SpacingAfter = 10;
            doc.Add(p);
        }

        static void AddSubSection(Document doc, string text, Font font)
        {
            Paragraph p = new Paragraph(text, font);
            p.Alignment = Element.ALIGN_RIGHT;
            p.SpacingBefore = 12;
            p.SpacingAfter = 6;
            p.IndentationRight = 15;
            doc.Add(p);
        }

        static void AddParagraph(Document doc, string text, Font font)
        {
            Paragraph p = new Paragraph(text, font);
            p.Alignment = Element.ALIGN_RIGHT;
            p.SpacingAfter = 8;
            p.IndentationRight = 15;
            doc.Add(p);
        }

        static void AddBullet(Document doc, string text, Font font)
        {
            Paragraph p = new Paragraph(text, font);
            p.Alignment = Element.ALIGN_RIGHT;
            p.SpacingAfter = 4;
            p.IndentationRight = 30;
            doc.Add(p);
        }
    }
}

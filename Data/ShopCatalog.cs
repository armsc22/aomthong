using GoldShopWeb.Models;

namespace GoldShopWeb.Data;

/// <summary>
/// In-memory catalog. Everything here is sample data meant to be replaced by the
/// shop's real products (or by a database) later.
/// </summary>
public static class ShopCatalog
{
    public static readonly IReadOnlyList<Category> Categories =
    [
        new() { Id = 1, Slug = "gold-bars",   Name = "ทองคำแท่ง",        Tagline = "ลงทุนทองแท่ง 96.5% และ 99.99%", Icon = "bar",      Image = "/img/products/gold-bar-1.svg" },
        new() { Id = 2, Slug = "necklaces",   Name = "สร้อยคอ",          Tagline = "สร้อยคอทองลายยอดนิยม",            Icon = "necklace", Image = "/img/products/necklace-1.svg" },
        new() { Id = 3, Slug = "rings",       Name = "แหวน",             Tagline = "แหวนทองและแหวนเพชร",             Icon = "ring",     Image = "/img/products/ring-1.svg" },
        new() { Id = 4, Slug = "bracelets",   Name = "กำไลและสร้อยข้อมือ", Tagline = "กำไลข้อมือ สร้อยข้อมือ",          Icon = "bracelet", Image = "/img/products/bracelet-1.svg" },
        new() { Id = 5, Slug = "earrings",    Name = "ต่างหู",           Tagline = "ต่างหูทองและต่างหูเพชร",         Icon = "earring",  Image = "/img/products/earring-1.svg" },
        new() { Id = 6, Slug = "diamonds",    Name = "เพชรและอัญมณี",     Tagline = "เพชรใบเซอร์ GIA",                Icon = "diamond",  Image = "/img/products/diamond-1.svg" },
        new() { Id = 7, Slug = "auspicious",  Name = "ของขวัญมงคล",       Tagline = "ของขวัญทองมงคลและของชำร่วย",      Icon = "gift",     Image = "/img/products/gift-1.svg" },
    ];

    public static readonly IReadOnlyList<Product> Products = BuildProducts();

    private static List<Product> BuildProducts()
    {
        var products = new List<Product>();

        // ---- ทองคำแท่ง (gold bars) — รายการจริง สต๊อกแท่งละ 5 ----
        var barSpecs = new (string Sku, string Weight, string Name, decimal Price)[]
        {
            ("GB-1S-00",  "1 สลึง (3.79 ก.)",   "ทองคำแท่ง 1 สลึง",  18240m),
            ("GB-2S-00",  "2 สลึง (7.58 ก.)",   "ทองคำแท่ง 2 สลึง",  36180m),
            ("GB-1B-00",  "1 บาท (15.24 ก.)",   "ทองคำแท่ง 1 บาท",   72060m),
            ("GB-2B-00",  "2 บาท (30.48 ก.)",   "ทองคำแท่ง 2 บาท",   143820m),
            ("GB-4B-00",  "4 บาท (60.96 ก.)",   "ทองคำแท่ง 4 บาท",   287340m),
            ("GB-5B-00",  "5 บาท (76.20 ก.)",   "ทองคำแท่ง 5 บาท",   359100m),
            ("GB-6B-00",  "6 บาท (91.44 ก.)",   "ทองคำแท่ง 6 บาท",   430860m),
            ("GB-7B-00",  "7 บาท (106.68 ก.)",  "ทองคำแท่ง 7 บาท",   502620m),
            ("GB-8B-00",  "8 บาท (121.92 ก.)",  "ทองคำแท่ง 8 บาท",   574380m),
            ("GB-9B-00",  "9 บาท (137.16 ก.)",  "ทองคำแท่ง 9 บาท",   646140m),
            ("GB-10B-00", "10 บาท (152.40 ก.)", "ทองคำแท่ง 10 บาท",  717900m),
        };
        var barImages = new[] { "/img/products/gold-bar-1.svg", "/img/products/gold-bar-2.svg", "/img/products/gold-bar-3.svg" };
        var barId = 100;
        for (var i = 0; i < barSpecs.Length; i++)
        {
            var s = barSpecs[i];
            products.Add(new Product
            {
                Id = ++barId,
                CategoryId = 1,
                Sku = s.Sku,
                Slug = s.Sku.ToLowerInvariant(),
                Name = s.Name,
                Weight = s.Weight,
                Price = s.Price,
                CraftingFee = 300m,
                Image = barImages[i % barImages.Length],
                ShortDescription = $"ทองแท่ง 96.5% {s.Weight} ค่าบล็อก 300 บาท",
                Description = $"{s.Name} ความบริสุทธิ์ 96.5% ผลิตจากโรงงานที่ได้รับการรับรองจากสมาคมค้าทองคำแห่งประเทศไทย ผิวเรียบสนิท มีรหัสกำกับทุกแท่ง น้ำหนัก {s.Weight} ค่าบล็อก 300 บาท มาพร้อมตลับพลาสติกกันรอยและใบรับประกัน ราคาอาจปรับเปลี่ยนตามราคาทองประจำวัน",
                Stock = 5,
                IsNew = s.Sku == "GB-1S-00",
                IsFeatured = s.Sku is "GB-1B-00" or "GB-5B-00",
                IsBestSeller = s.Sku == "GB-1B-00",
            });
        }

        // ---- ทองรูปพรรณ (สร้อยคอลายต่างๆ) — รายการจริง สต๊อกเส้นละ 5 ----
        var patterns = new[] { "คตกิตทราย", "ทาโร่", "ผ่าหวาย", "ห่วงคู่", "โซ่เรือตัน", "ผ่าหวายตัน", "ซีตรองตัน" };
        var jwSpecs = new (string Prefix, string WeightShort, string Grams, decimal Fee, decimal Price)[]
        {
            ("GP-1S", "1 สลึง", "3.79 กรัม",  280m,  18430m),
            ("GP-2S", "2 สลึง", "7.58 กรัม",  350m,  36650m),
            ("GP-1B", "1 บาท",  "15.24 กรัม", 450m,  73050m),
            ("GP-2B", "2 บาท",  "30.48 กรัม", 600m,  145800m),
            ("GP-3B", "3 บาท",  "45.48 กรัม", 850m,  218650m),
            ("GP-5B", "5 บาท",  "76.22 กรัม", 1100m, 364100m),
        };
        var necklaceImages = new[] { "/img/products/necklace-1.svg", "/img/products/necklace-2.svg", "/img/products/necklace-3.svg" };
        var jwId = 200;
        foreach (var w in jwSpecs)
        {
            for (var p = 0; p < patterns.Length; p++)
            {
                var sku = $"{w.Prefix}-{p + 1:D2}";
                var pattern = patterns[p];
                products.Add(new Product
                {
                    Id = ++jwId,
                    CategoryId = 2,
                    Sku = sku,
                    Slug = sku.ToLowerInvariant(),
                    Name = $"สร้อยคอลาย{pattern} {w.WeightShort}",
                    Weight = $"{w.WeightShort} ({w.Grams})",
                    Price = w.Price,
                    CraftingFee = w.Fee,
                    Image = necklaceImages[(jwId - 201) % necklaceImages.Length],
                    ShortDescription = $"ลาย{pattern} น้ำหนัก {w.WeightShort} ค่ากำเหน็จ {w.Fee:N0} บาท",
                    Description = $"สร้อยคอทองคำ 96.5% ลาย{pattern} น้ำหนัก {w.WeightShort} ({w.Grams}) ถักแน่น ผิวเงาสวย ตัวล็อกแข็งแรงทนทาน ใส่ได้ทุกวัน ค่ากำเหน็จ {w.Fee:N0} บาท ราคาอาจปรับเปลี่ยนตามราคาทองประจำวัน",
                    Stock = 5,
                    IsNew = sku is "GP-1S-01" or "GP-2S-01",
                    IsFeatured = sku is "GP-1B-01" or "GP-5B-05",
                    IsBestSeller = sku is "GP-1B-01" or "GP-2S-03",
                });
            }
        }

        // ---- หมวดตัวอย่างอื่นๆ (ยังไม่มีรายการจริงจากทางร้าน) ----
        products.AddRange(new[]
        {
            // แหวน
            new Product { Id = 301, CategoryId = 3, Slug = "ring-diamond-halo-50", Name = "แหวนเพชร Halo 0.50 กะรัต", Weight = "0.50 กะรัต", Purity = "ทองขาว 75%", Price = 89000m, OldPrice = 99000m, Image = "/img/products/ring-1.svg", ShortDescription = "เพชรเม็ดกลางล้อมเพชรเล็ก ไฟสวยจัด", Description = "แหวนทองขาว 18K ประดับเพชรเม็ดกลางน้ำหนัก 0.50 กะรัต น้ำ 97 (G color) ความสะอาดระดับ VS1 ล้อมด้วยเพชรเม็ดเล็กอีก 0.15 กะรัต ตัวเรือนขัดเงาละเอียด มาพร้อมใบรับรองเพชร", IsFeatured = true, IsBestSeller = true, Stock = 4 },
            new Product { Id = 302, CategoryId = 3, Slug = "ring-gold-plain-1-salueng", Name = "แหวนทองเกลี้ยง 1 สลึง", Weight = "1 สลึง", Price = 14200m, Image = "/img/products/ring-2.svg", ShortDescription = "แหวนเกลี้ยงคลาสสิก ใส่ได้ทุกวัย", Description = "แหวนทองคำ 96.5% เกลี้ยงเรียบ ผิวเงางาม น้ำหนัก 1 สลึง มีหลายไซซ์ให้เลือก ดีไซน์คลาสสิกใส่ได้ทุกวัน เป็นที่นิยมสำหรับซื้อเป็นของขวัญ", IsNew = true, Stock = 42 },
            new Product { Id = 303, CategoryId = 3, Slug = "ring-gold-flower-2-salueng", Name = "แหวนทองลายดอกไม้ 2 สลึง", Weight = "2 สลึง", Price = 28400m, Image = "/img/products/ring-3.svg", ShortDescription = "ลายดอกไม้ฉลุโปร่ง น้ำหนักเบา", Description = "แหวนทองคำ 96.5% ลายดอกไม้ฉลุโปร่ง เทคนิคการถักแบบโบราณ ทำให้น้ำหนักเบาแต่ดูใหญ่สวยงาม น้ำหนัก 2 สลึง ใส่สบายไม่หนักมือ", Stock = 11 },
            new Product { Id = 304, CategoryId = 3, Slug = "ring-mens-signet-1-baht", Name = "แหวนผู้ชายลายหัวสิงห์ 1 บาท", Weight = "1 บาท", Price = 56800m, Image = "/img/products/ring-2.svg", ShortDescription = "แหวนหัวใหญ่ สำหรับสุภาพบุรุษ", Description = "แหวนทองคำ 96.5% สำหรับสุภาพบุรุษ หัวแหวนขนาดใหญ่แกะลายละเอียด น้ำหนัก 1 บาท ตัวเรือนหนาแข็งแรง ให้ลุคภูมิฐาน", Stock = 7 },

            // กำไลและสร้อยข้อมือ
            new Product { Id = 401, CategoryId = 4, Slug = "bracelet-lai-takrut-1-baht", Name = "สร้อยข้อมือลายตะกรุด 1 บาท", Weight = "1 บาท", Price = 55600m, OldPrice = 57000m, Image = "/img/products/bracelet-1.svg", ShortDescription = "ลายตะกรุดถักแน่น มงคลและทนทาน", Description = "สร้อยข้อมือทองคำ 96.5% ลายตะกรุด ถักแน่นตลอดเส้น ความยาว 7.5 นิ้ว น้ำหนัก 1 บาท ตัวล็อกแบบกล่องพร้อมเซฟตี้ล็อกแข็งแรง เชื่อกันว่าเป็นลายมงคล", IsBestSeller = true, Stock = 13 },
            new Product { Id = 402, CategoryId = 4, Slug = "bangle-gold-2-salueng", Name = "กำไลทองเกลี้ยง 2 สลึง", Weight = "2 สลึง", Price = 28600m, Image = "/img/products/bracelet-2.svg", ShortDescription = "กำไลเกลี้ยง ทรงกลม ใส่คู่ได้", Description = "กำไลทองคำ 96.5% เกลี้ยงทรงกลม น้ำหนัก 2 สลึง ผิวขัดเงาเรียบลื่น สวมใส่ง่าย ใส่เดี่ยวหรือใส่ซ้อนกันหลายวงก็สวย", IsNew = true, Stock = 22 },
            new Product { Id = 403, CategoryId = 4, Slug = "bracelet-lai-chan-2-salueng", Name = "สร้อยข้อมือลายชาน 2 สลึง", Weight = "2 สลึง", Price = 28300m, Image = "/img/products/bracelet-3.svg", ShortDescription = "ลายชานขนาดข้อมือ", Description = "สร้อยข้อมือทองคำ 96.5% ลายชาน ความยาว 7 นิ้ว น้ำหนัก 2 สลึง ถักแน่นทนทาน ตัวล็อกก้ามปูใช้งานง่าย", Stock = 16 },
            new Product { Id = 404, CategoryId = 4, Slug = "bangle-diamond-cut-1-baht", Name = "กำไลเพชรซีก 1 บาท", Weight = "1 บาท", Price = 62000m, Image = "/img/products/bracelet-1.svg", ShortDescription = "กำไลประดับเพชรซีก ระยิบระยับ", Description = "กำไลทองคำ 96.5% ฝังเพชรซีกเรียงรอบตัวเรือน น้ำหนัก 1 บาท เล่นไฟสวยงามทุกมุมมอง เหมาะสำหรับโอกาสพิเศษ", IsFeatured = true, Stock = 5 },

            // ต่างหู
            new Product { Id = 501, CategoryId = 5, Slug = "earring-gold-round-1-salueng", Name = "ต่างหูทองกลมเกลี้ยง 1 สลึง", Weight = "1 สลึง", Price = 14600m, Image = "/img/products/earring-1.svg", ShortDescription = "ต่างหูเกลี้ยง น้ำหนักเบา ใส่สบาย", Description = "ต่างหูทองคำ 96.5% ทรงกลมเกลี้ยง น้ำหนัก 1 สลึง (คู่) ผิวเงาเรียบ ก้านต่างหูแข็งแรง ใส่ได้ตลอดวันโดยไม่ระคายเคือง", IsNew = true, Stock = 35 },
            new Product { Id = 502, CategoryId = 5, Slug = "earring-diamond-stud", Name = "ต่างหูเพชรเม็ดกลม 0.30 กะรัต", Weight = "0.30 กะรัต (คู่)", Purity = "ทองขาว 75%", Price = 42000m, OldPrice = 46500m, Image = "/img/products/earring-2.svg", ShortDescription = "เพชรเม็ดกลมจับคู่ ไฟสวยเท่ากัน", Description = "ต่างหูทองขาว 18K ประดับเพชรเม็ดกลมข้างละ 0.15 กะรัตรวม 0.30 กะรัต น้ำ 97 ความสะอาด VS คัดคู่ให้ขนาดและไฟใกล้เคียงกัน มาพร้อมใบรับรอง", IsFeatured = true, IsBestSeller = true, Stock = 8 },
            new Product { Id = 503, CategoryId = 5, Slug = "earring-gold-drop-2-salueng", Name = "ต่างหูทองห้อยลายดอกไม้ 2 สลึง", Weight = "2 สลึง", Price = 28900m, Image = "/img/products/earring-3.svg", ShortDescription = "ต่างหูห้อยระย้า ลายดอกไม้", Description = "ต่างหูทองคำ 96.5% แบบห้อยระย้า ประดับลายดอกไม้ฉลุโปร่ง น้ำหนัก 2 สลึง (คู่) ให้ความอ่อนช้อยเวลาเคลื่อนไหว", Stock = 14 },
            new Product { Id = 504, CategoryId = 5, Slug = "earring-gold-hoop-1-salueng", Name = "ต่างหูทองห่วง 1 สลึง", Weight = "1 สลึง", Price = 14900m, Image = "/img/products/earring-1.svg", ShortDescription = "ต่างหูห่วงทรงทันสมัย", Description = "ต่างหูทองคำ 96.5% ทรงห่วง เส้นผ่านศูนย์กลาง 2 ซม. น้ำหนัก 1 สลึง (คู่) ดีไซน์มินิมอลใส่ได้ทุกวัน", Stock = 26 },

            // เพชรและอัญมณี
            new Product { Id = 601, CategoryId = 6, Slug = "diamond-loose-1ct-gia", Name = "เพชรเม็ดเปลือย 1.00 กะรัต GIA", Weight = "1.00 กะรัต", Purity = "เพชรธรรมชาติ", Price = 285000m, Image = "/img/products/diamond-1.svg", ShortDescription = "เพชรใบเซอร์ GIA น้ำ 97 ความสะอาด VVS1", Description = "เพชรเม็ดเปลือยทรงกลม Brilliant Cut น้ำหนัก 1.00 กะรัต น้ำ 97 (G Color) ความสะอาด VVS1 การเจียระไนระดับ 3EX มาพร้อมใบรับรองจากสถาบัน GIA สามารถสั่งฝังตัวเรือนได้ตามต้องการ", IsFeatured = true, Stock = 2 },
            new Product { Id = 602, CategoryId = 6, Slug = "pendant-diamond-drop", Name = "จี้เพชรหยดน้ำ ทองขาว", Weight = "0.40 กะรัต", Purity = "ทองขาว 75%", Price = 56000m, OldPrice = 62000m, Image = "/img/products/diamond-2.svg", ShortDescription = "จี้เพชรทรงหยดน้ำ ไฟสวย", Description = "จี้ทองขาว 18K ประดับเพชรทรงหยดน้ำน้ำหนัก 0.40 กะรัต ล้อมเพชรเม็ดเล็กรอบตัวเรือน มาพร้อมสร้อยทองขาวความยาว 18 นิ้ว", IsNew = true, Stock = 6 },
            new Product { Id = 603, CategoryId = 6, Slug = "gemstone-ruby-ring", Name = "แหวนทับทิมล้อมเพชร", Weight = "ทับทิม 2.10 กะรัต", Purity = "ทอง 90%", Price = 74000m, Image = "/img/products/diamond-3.svg", ShortDescription = "ทับทิมสยามสีแดงเข้ม ล้อมเพชร", Description = "แหวนทองคำ 90% ประดับทับทิมสยามน้ำหนัก 2.10 กะรัต สีแดงเข้มสม่ำเสมอ ล้อมด้วยเพชรเม็ดเล็กรวม 0.35 กะรัต งานฝังประณีตแบบโบราณ", Stock = 3 },
            new Product { Id = 604, CategoryId = 6, Slug = "gemstone-emerald-pendant", Name = "จี้มรกตโคลอมเบีย", Weight = "มรกต 1.80 กะรัต", Purity = "ทอง 90%", Price = 98000m, Image = "/img/products/diamond-2.svg", ShortDescription = "มรกตสีเขียวสด งานฝังทอง", Description = "จี้ทองคำ 90% ประดับมรกตโคลอมเบียน้ำหนัก 1.80 กะรัต สีเขียวสดชัด ล้อมเพชรเม็ดเล็ก ตัวเรือนขัดเงาสองด้าน มาพร้อมกล่องกำมะหยี่", Stock = 2 },

            // ของขวัญมงคล
            new Product { Id = 701, CategoryId = 7, Slug = "gift-gold-orchid", Name = "กล้วยไม้ทองชุบ 24K", Weight = "ดอกกล้วยไม้จริงชุบทอง", Purity = "24K ชุบทอง", Price = 3900m, Image = "/img/products/gift-1.svg", ShortDescription = "ของขวัญมงคล งานแฮนด์เมด", Description = "ดอกกล้วยไม้จริงผ่านกระบวนการเคลือบและชุบทองคำ 24K ทุกดอกเป็นชิ้นงานเดียวในโลก มาพร้อมแจกันแก้วและกล่องของขวัญหรูหรา เหมาะสำหรับมอบในโอกาสพิเศษ", IsBestSeller = true, Stock = 40 },
            new Product { Id = 702, CategoryId = 7, Slug = "gift-gold-bar-card-1g", Name = "ทองคำแผ่นการ์ดลายมงคล 1 กรัม", Weight = "1 กรัม", Purity = "99.99%", Price = 4200m, OldPrice = 4600m, Image = "/img/products/gift-2.svg", ShortDescription = "การ์ดทองลายมงคล ให้เป็นของขวัญ", Description = "ทองคำแผ่นบริสุทธิ์ 99.99% น้ำหนัก 1 กรัม บรรจุในการ์ดพิมพ์ลายมงคลพร้อมช่องใส สามารถเขียนข้อความอวยพรได้ เหมาะเป็นของขวัญปีใหม่ ของรับปริญญา หรือของชำร่วยงานแต่ง", IsNew = true, Stock = 88 },
            new Product { Id = 703, CategoryId = 7, Slug = "gift-gold-pixiu", Name = "ปี่เซียะทอง เรียกทรัพย์", Weight = "3.75 กรัม", Purity = "ทอง 99.9%", Price = 8900m, Image = "/img/products/gift-3.svg", ShortDescription = "ปี่เซียะทองมงคล เสริมโชคลาภ", Description = "ปี่เซียะทองคำบริสุทธิ์ 99.9% น้ำหนัก 3.75 กรัม แกะลายละเอียด ตั้งบนฐานไม้ พร้อมกล่องกำมะหยี่สีแดง เชื่อกันว่าเป็นสัตว์มงคลช่วยเรียกทรัพย์และป้องกันสิ่งไม่ดี", IsFeatured = true, Stock = 19 },
            new Product { Id = 704, CategoryId = 7, Slug = "gift-baby-set", Name = "ชุดของขวัญทารก ทองคำ 1 สลึง", Weight = "1 สลึง (เซต 3 ชิ้น)", Price = 16500m, Image = "/img/products/gift-1.svg", ShortDescription = "เซตกำไล สร้อย และจี้ สำหรับเด็ก", Description = "ชุดของขวัญทองคำ 96.5% สำหรับทารก ประกอบด้วยกำไลข้อเท้ากระดิ่ง 1 คู่ สร้อยคอ และจี้ลายการ์ตูน รวม 1 สลึง บรรจุในกล่องของขวัญบุกำมะหยี่ พร้อมการ์ดอวยพร", Stock = 10 },
        });

        return products;
    }

    public static readonly IReadOnlyList<Banner> Banners =
    [
        new()
        {
            Badge = "โปรโมชั่นประจำเดือน",
            Title = "ทองแท่ง 96.5% ผ่อน 0% นาน 10 เดือน",
            Subtitle = "ออมทองได้ทุกเดือน เริ่มเพียงเดือนละ 5,435 บาท พร้อมรับซื้อคืนตามราคาประกาศสมาคม",
            CtaText = "ดูทองแท่งทั้งหมด",
            CtaUrl = "/Products/gold-bars",
            Image = "/img/banners/hero-1.svg",
            Theme = "dark",
        },
        new()
        {
            Badge = "คอลเลกชันใหม่",
            Title = "เครื่องประดับเพชร ใบเซอร์ GIA ทุกเม็ด",
            Subtitle = "คัดเพชรน้ำ 97 ขึ้นไป การเจียระไนระดับ 3EX พร้อมบริการล้างและขัดเงาฟรีตลอดอายุการใช้งาน",
            CtaText = "ชมคอลเลกชันเพชร",
            CtaUrl = "/Products/diamonds",
            Image = "/img/banners/hero-2.svg",
            Theme = "light",
        },
        new()
        {
            Badge = "ของขวัญมงคล",
            Title = "การ์ดทองลายมงคล เริ่มต้น 4,200 บาท",
            Subtitle = "ของขวัญที่มีความหมายในทุกโอกาส สั่งพิมพ์ข้อความลงการ์ดได้ฟรี ไม่มีขั้นต่ำ",
            CtaText = "เลือกของขวัญ",
            CtaUrl = "/Products/auspicious",
            Image = "/img/banners/hero-3.svg",
            Theme = "gold",
        },
    ];

    public static readonly IReadOnlyList<Banner> PromoTiles =
    [
        new() { Title = "รับซื้อทองเก่า ราคาสูง", Subtitle = "ตีราคาฟรีหน้าร้าน ไม่มีค่าธรรมเนียม", CtaText = "รายละเอียด", CtaUrl = "/Home/Contact", Image = "/img/banners/promo-1.svg" },
        new() { Title = "ผ่อนทอง 0% นาน 10 เดือน", Subtitle = "กับบัตรเครดิตที่ร่วมรายการ", CtaText = "ดูเงื่อนไข", CtaUrl = "/Home/Contact", Image = "/img/banners/promo-2.svg" },
    ];

    public static readonly IReadOnlyList<SavingsPlan> SavingsPlans =
    [
        new()
        {
            Id = 1,
            Name = "ออมทองเริ่มต้น",
            MinDeposit = 1000m,
            Months = 12,
            InterestRate = 9.9m,
            Description = "เหมาะกับผู้เริ่มออมเงินก้อนเล็กสม่ำเสมอทุกเดือน",
            Perks = ["เปิดบัญชีขั้นต่ำ 1,000 บาท", "ฝากเพิ่มได้ทุกเดือนไม่มีขั้นต่ำ", "แลกทองแท่งหรือทองรูปพรรณได้เมื่อครบกำหนด"],
        },
        new()
        {
            Id = 2,
            Name = "ออมทองทวีทรัพย์",
            MinDeposit = 5000m,
            Months = 24,
            InterestRate = 9.9m,
            Description = "แผนยอดนิยม รับสิทธิแลกทองก่อนครบกำหนด",
            IsPopular = true,
            Perks = ["เปิดบัญชีขั้นต่ำ 5,000 บาท", "ปันผล 9.9% ต่อปี คิดสะสมรายวัน", "ขอแลกทองก่อนครบกำหนดได้ตั้งแต่เดือนที่ 12", "แจ้งยอดและใบเสร็จผ่าน LINE อัตโนมัติ"],
        },
        new()
        {
            Id = 3,
            Name = "ออมทองมั่งคั่ง",
            MinDeposit = 20000m,
            Months = 36,
            InterestRate = 9.9m,
            Description = "สำหรับผู้ที่ต้องการสะสมทองปริมาณมากในระยะยาว",
            Perks = ["เปิดบัญชีขั้นต่ำ 20,000 บาท", "ปันผล 9.9% ต่อปี คิดสะสมรายวัน", "ฟรีค่าธรรมเนียมการแลกทองทุกประเภท", "มีเจ้าหน้าที่ดูแลบัญชีส่วนตัว"],
        },
    ];

    public static readonly IReadOnlyList<SavingsStep> SavingsSteps =
    [
        new() { Title = "สมัครสมาชิก", Detail = "กรอกข้อมูลและยืนยันตัวตนด้วยเลขบัตรประชาชน ใช้เวลาประมาณ 5 นาที" },
        new() { Title = "เปิดบัญชีออมทอง", Detail = "เลือกแผนการออมและฝากเงินเปิดบัญชีขั้นต่ำตามแผน" },
        new() { Title = "ฝากเงินสะสม", Detail = "ฝากเพิ่มได้ทุกช่องทาง ยอดเงินและดอกเบี้ยจะแสดงในสมุดฝากทองออนไลน์" },
        new() { Title = "แลกทองหรือถอน", Detail = "เมื่อครบกำหนดแลกเป็นทองแท่ง ทองรูปพรรณ หรือถอนเป็นเงินสดได้" },
    ];

    public static readonly IReadOnlyList<SavingsFaq> SavingsFaqs =
    [
        new() { Question = "เปิดบัญชีออมทองต้องใช้เอกสารอะไร?", Answer = "ใช้บัตรประชาชนใบเดียวสำหรับบุคคลธรรมดา หากเป็นนิติบุคคลใช้หนังสือรับรองบริษัทและบัตรประชาชนผู้มีอำนาจลงนาม" },
        new() { Question = "ฝากเงินขั้นต่ำครั้งละเท่าไร?", Answer = "เงินฝากเปิดบัญชีขั้นต่ำตามแผนที่เลือก หลังจากนั้นฝากเพิ่มได้โดยไม่กำหนดขั้นต่ำ" },
        new() { Question = "ดอกเบี้ยคิดอย่างไรและเข้าเมื่อไร?", Answer = "คิดจากยอดคงเหลือเฉลี่ยรายวัน จ่ายเข้าบัญชีเงินฝากทุกสิ้นเดือนตามอัตราดอกเบี้ยของแผนที่เลือก" },
        new() { Question = "ถอนเงินก่อนครบกำหนดได้ไหม?", Answer = "ถอนได้โดยยอดดอกเบี้ยจะปรับตามระยะเวลาที่ฝากจริง ไม่เสียค่าธรรมเนียมการถอน" },
        new() { Question = "แลกทองต้องทำที่สาขาเท่านั้นหรือไม่?", Answer = "แจ้งความประสงค์ผ่านหน้าเว็บหรือ LINE ได้ จากนั้นนำใบยืนยันมาแลกทองที่สาขาใดก็ได้" },
    ];

    public static IReadOnlyList<GoldPrice> GoldPrices()
    {
        var stamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm น.");
        return
        [
            new GoldPrice { Label = "ทองแท่ง 96.5%",  Buy = 54250.00m, Sell = 54350.00m, Change = 150m,  UpdatedAt = stamp },
            new GoldPrice { Label = "ทองรูปพรรณ 96.5%", Buy = 53168.48m, Sell = 54850.00m, Change = 150m,  UpdatedAt = stamp },
            new GoldPrice { Label = "ทองแท่ง 99.99%",  Buy = 56180.00m, Sell = 56380.00m, Change = -50m, UpdatedAt = stamp },
        ];
    }

    public static Product? FindBySlug(string slug) => Products.FirstOrDefault(p => p.Slug == slug);

    public static Product? FindById(int id) => Products.FirstOrDefault(p => p.Id == id);

    public static Category? FindCategory(int id) => Categories.FirstOrDefault(c => c.Id == id);

    public static Category? FindCategoryBySlug(string slug) => Categories.FirstOrDefault(c => c.Slug == slug);

    public static IReadOnlyList<Product> ByCategory(int categoryId) =>
        Products.Where(p => p.CategoryId == categoryId).ToList();

    public static IReadOnlyList<Product> Related(Product product, int take = 4) =>
        Products.Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id).Take(take).ToList();

    public static IReadOnlyList<Product> Search(string term)
    {
        var t = term.Trim().ToLowerInvariant();
        return Products
            .Where(p => p.Name.ToLowerInvariant().Contains(t)
                     || p.ShortDescription.ToLowerInvariant().Contains(t))
            .ToList();
    }

    /// <summary>Filters and sorts the catalog for the listing page.</summary>
    public static IReadOnlyList<Product> Query(string? categorySlug, string? search, string sort)
    {
        IEnumerable<Product> query = Products;

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var cat = FindCategoryBySlug(categorySlug);
            if (cat is not null)
            {
                query = query.Where(p => p.CategoryId == cat.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var ids = Search(search).Select(p => p.Id).ToHashSet();
            query = query.Where(p => ids.Contains(p.Id));
        }

        return sort switch
        {
            "price-asc" => query.OrderBy(p => p.Price).ToList(),
            "price-desc" => query.OrderByDescending(p => p.Price).ToList(),
            "newest" => query.OrderByDescending(p => p.IsNew).ThenBy(p => p.Id).ToList(),
            "name" => query.OrderBy(p => p.Name, StringComparer.CurrentCulture).ToList(),
            _ => query.OrderByDescending(p => p.IsBestSeller).ThenByDescending(p => p.IsFeatured).ThenBy(p => p.Id).ToList(),
        };
    }
}

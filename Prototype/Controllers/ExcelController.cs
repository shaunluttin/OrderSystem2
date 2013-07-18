﻿using LinqToExcel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Prototype.ViewModels;
using System.Reflection;

namespace Prototype.Controllers
{
    public class ExcelController : Controller
    {
        private string ExcelDirectory
        {
            get
            {
                return Server.MapPath("~/ExcelWorkbooks");
            }
        }

        public ActionResult Index()
        {
            IEnumerable<System.String> workbookNames = from file in Directory.EnumerateFiles(ExcelDirectory, "*.xlsx", SearchOption.AllDirectories)
                                                       select System.IO.Path.GetFileName(file);
            return View(workbookNames);
        }

        public ActionResult Worksheets(string workbookName)
        {
            ViewBag.WorkbookName = workbookName;
            var excel = new ExcelQueryFactory(Path.Combine(ExcelDirectory, workbookName));
            IEnumerable<System.String> worksheetNames = excel.GetWorksheetNames();
            return View("Worksheets", worksheetNames);
        }

        public ActionResult Columns(string workbookName, string worksheetName)
        {
            ViewBag.WorkbookName = workbookName;
            ViewBag.WorksheetName = worksheetName;
            var excel = new ExcelQueryFactory(Path.Combine(ExcelDirectory, workbookName));
            IEnumerable<System.String> excelColumnNames = excel.GetColumnNames(worksheetName);

            ExcelColumnToProductPropertyMappingChoices mappingChoices = new ExcelColumnToProductPropertyMappingChoices();
            mappingChoices.ExcelColumns = excelColumnNames.ToList<String>();
            PropertyInfo[] simpleProduct = typeof(SimpleProduct).GetProperties();
            foreach (PropertyInfo pi in simpleProduct)
            {
                mappingChoices.ProductProperties.Add(pi.Name);
            }

            return View(mappingChoices);
        }

        [HttpPost]
        public ActionResult Data_BeforeImport(string workbookName, string worksheetName, SimpleProduct mappings)
        {
            string[] stringsToAvoid = { "$ DECREASE", "$ INCREASE", "NEW", "DISCONTINUED" };

            ViewBag.WorkbookName = workbookName;
            ViewBag.WorksheetName = worksheetName;

            //TODO Can we use a typed form or pass a typed object?
            var excel = new ExcelQueryFactory(Path.Combine(ExcelDirectory, workbookName));
            var rows = excel.Worksheet(worksheetName);

            ////create vendor product list
            SimpleVendor simpleVendor = new SimpleVendor();
            simpleVendor.VendorName = "test vendor";
            simpleVendor.VendorID = 0;

            //add rows to datatable            
            foreach (LinqToExcel.Row r in rows)
            {
                SimpleProduct product = new SimpleProduct();

                product.ProductName = r[mappings.ProductName];
                product.ProductDescription = r[mappings.ProductDescription];

                PropertyInfo[] properties = typeof(SimpleProduct).GetProperties();
                bool hasData = properties.Any<PropertyInfo>(pi =>
                    pi.GetValue(product) != null &&
                    pi.GetValue(product).ToString().Length > 0 &&
                    !stringsToAvoid.Any<String>(pi.GetValue(product).ToString().Contains));                

                if (hasData)
                {
                    simpleVendor.Products.Add(product);
                }
            }

            return View(simpleVendor);
        }

        [HttpPost]
        public ActionResult Data_AfterImport(SimpleVendor simpleVendor)
        {
            //create the product list
            List<Models.Product> products = new List<Models.Product>();
            AutoMapper.Mapper.CreateMap<ViewModels.SimpleProduct, Models.Product>();
            foreach (SimpleProduct p in simpleVendor.Products)
            {
                Models.Product product = AutoMapper.Mapper.Map<ViewModels.SimpleProduct, Models.Product>(p);
                products.Add(product);
            }

            //update the database
            using (Models.VendordContext db = new Prototype.Models.VendordContext())
            {                
                //get the vendor by id from the db
                Models.Vendor vendor = db.Vendors.Where<Models.Vendor>(v => v.VendorID == simpleVendor.VendorID).FirstOrDefault<Models.Vendor>();
                if (vendor == null)
                {
                    //create new vendor if not exists
                    vendor = db.Vendors.Create();
                    db.Vendors.Add(vendor);
                }
                //update the vendor
                vendor.VendorName = simpleVendor.VendorName;
                vendor.Products = products;
                //save                
                db.SaveChanges();                
            }
            return View();
        }
    }
}

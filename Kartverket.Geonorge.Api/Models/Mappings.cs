using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Kartverket.Geonorge.Api.Models
{
    public class Mappings
    {
        public static readonly Dictionary<string, string> ThemeNationalToEU = new Dictionary<string, string>()
        {
            {"Basis geodata", "REGI"},
            {"Befolkning", "SOCI"},
            {"Energi", "ENER"},
            {"Forurensning", "ENVI"},
            {"Friluftsliv", "HEAL"},
            {"Geologi", "REGI"},
            {"Kulturminner", "EDUC"},
            {"Kyst og fiskeri", "AGRI"},
            {"Landbruk", "AGRI"},
            {"Landskap", "ENVI"},
            {"Natur", "ENVI"},
            {"Plan", "GOVE"},
            {"Samferdsel", "TRAN"},
            {"Samfunnssikkerhet", "JUST"}
        };

        public static readonly Dictionary<string, string> ThemeInspireToEU = new Dictionary<string, string>()
        {
            {"Addresses", "REGI"},
            {"Administrative units", "GOVE"},
            {"Agricultural and aquaculture facilities", "AGRI"},
            {"Area management/restriction/regulation zones and reporting units", "ENVI"},
            {"Atmospheric conditions", "ENVI"},
            {"Bio-geographical regions", "ENVI"},
            {"Buildings", "REGI"},
            {"Cadastral parcels", "REGI"},
            {"Coordinate reference systems", "REGI"},
            {"Elevation", "REGI"},
            {"Energy resources", "ENER"},
            {"Environmental monitoring facilities", "ENVI"},
            {"Geographical grid systems", "REGI"},
            {"Geographical names", "REGI"},
            {"Geology", "REGI"},
            {"Habitats and biotopes", "ENVI"},
            {"Human health and safety", "HEAL"},
            {"Hydrography", "ENVI"},
            {"Land cover", "ENVI"},
            {"Land use", "ENVI"},
            {"Meteorological geographical features", "ENVI"},
            {"Mineral resources", "ECON"},
            {"Natural risk zones", "ENVI"},
            {"Oceanographic geographical features", "ENVI"},
            {"Orthoimagery", "REGI"},
            {"Population distribution - demography", "SOCI"},
            {"Production and industrial facilities", "ECON"},
            {"Protected sites", "ENVI"},
            {"Sea regions", "ENVI"},
            {"Soil", "ENVI"},
            {"Species distribution", "ENVI"},
            {"Statistical units", "SOCI"},
            {"Transport networks", "TRAN"},
            {"Utility and governmental services", "GOVE"}

        };
    }
}
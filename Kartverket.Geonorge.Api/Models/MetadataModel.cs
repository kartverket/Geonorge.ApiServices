using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Kartverket.Geonorge.Api.Models
{
    public class MetadataModel
    {
        [Required(ErrorMessage = "Tittel er påkrevd")]
        public string Title { get; set; }
        [Required(ErrorMessage = "Beskrivelse er påkrevd.")]
        public string Description { get; set; }
        [Required(ErrorMessage = "Navn er påkrevd.")]
        public string ContactName { get; set; }

        [Required(ErrorMessage = "Epost er påkrevd.")]
        [EmailAddress(ErrorMessage = "Epost-adressen er ugyldig")]
        public string ContactEmail { get; set; }
        [Required(ErrorMessage = "Organisasjon er påkrevd.")]
        public string ContactOrganization { get; set; }
    }
}
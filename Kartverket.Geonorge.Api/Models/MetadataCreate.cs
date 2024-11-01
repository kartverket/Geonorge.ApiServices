using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Kartverket.Geonorge.Api.Models
{
    public class MetadataCreate
    {
        [Required(ErrorMessage = "Tittel er påkrevd")]
        public string Title { get; set; }
        public string Uuid { get; set; }
        public string Description { get; set; }

        public string MetadataContactName { get; set; }

        [Required(ErrorMessage = "Epost er påkrevd.")]
        [EmailAddress(ErrorMessage = "Epost-adressen er ugyldig")]
        public string MetadataContactEmail { get; set; }

        public string MetadataContactOrganization { get; set; }
    }
}
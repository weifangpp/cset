//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DataLayer
{
    using System;
    using System.Collections.Generic;
    
    public partial class DEMOGRAPHICS_ASSET_VALUES
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DEMOGRAPHICS_ASSET_VALUES()
        {
            this.DEMOGRAPHICS = new HashSet<DEMOGRAPHIC>();
        }
    
        public int DemographicsAssetId { get; set; }
        public string AssetValue { get; set; }
        public Nullable<int> ValueOrder { get; set; }
        public string Standard { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DEMOGRAPHIC> DEMOGRAPHICS { get; set; }
    }
}

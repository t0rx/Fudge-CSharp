/* <!--
 * Copyright (C) 2009 - 2010 by OpenGamma Inc. and other contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * -->
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fudge.Encodings
{
    /// <summary>
    /// Tunable paramters for the JSON encoding/decoding.  Please refer to http://wiki.fudgemsg.org/display/FDG/JSON+Fudge+Messages for details on the representation.
    /// </summary>
    public class JSONSettings
    {
        /// <summary>Default name for the processing directives field.</summary>
        public const string DefaultProcessingDirectivesField = "fudgeProcessingDirectives";

        /// <summary>Default name for the schema version field.</summary>
        public const string DefaultSchemaVersionField = "fudgeSchemaVersion";

        /// <summary>Default name for the schema version field.</summary>
        public const string DefaultTaxonomyField = "fudgeTaxonomy";

        /// <summary>Property of the <see cref="FudgeContext"/> that holds the <see cref="JSONSettings"/> if non-default.</summary>
        public static readonly FudgeContextProperty JSONSettingsProperty = new FudgeContextProperty("Encodings.JSONSettings", typeof(JSONSettings));

        /// <summary>
        /// Constructs a new settings object with the default values.
        /// </summary>
        public JSONSettings()
        {
            ProcessingDirectivesField = DefaultProcessingDirectivesField;
            SchemaVersionField = DefaultSchemaVersionField;
            TaxonomyField = DefaultTaxonomyField;
            PreferFieldNames = true;
            NumbersAreOrdinals = true;
        }

        /// <summary>
        /// Clones an existing settings object.
        /// </summary>
        /// <param name="other">Object to clone</param>
        public JSONSettings(JSONSettings other)
        {
        }

        /// <summary>Gets or sets the name of the field to use for the processing directives, or <c>null</c> if it is to be omitted.</summary>
        public string ProcessingDirectivesField { get; set; }

        /// <summary>Gets or sets the name of the field to use for the schema version, or <c>null</c> if it is to be omitted.</summary>
        public string SchemaVersionField { get; set; }

        /// <summary>Gets or sets the name of the field to use for the taxonomy, or <c>null</c> if it is to be omitted.</summary>
        public string TaxonomyField { get; set; }

        /// <summary>Gets or sets whether field names are preferred over ordinals when encoding.</summary>
        public bool PreferFieldNames { get; set; }

        /// <summary>Gets or sets whether JSON fields names that are numbers are treated by default as ordinals rather than field names.</summary>
        public bool NumbersAreOrdinals { get; set; }
    }
}

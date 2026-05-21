// Copyright 2012-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "uris.h"

#include <lv2/atom/atom.h>
#include <lv2/core/lv2.h>
#include <lv2/event/event.h>
#include <lv2/presets/presets.h>
#include <lv2/state/state.h>
#include <lv2/ui/ui.h>
#include <sord/sord.h>

#include <stddef.h>
#include <stdint.h>

#define NS_DCTERMS "http://purl.org/dc/terms/"
#define NS_DYNMAN "http://lv2plug.in/ns/ext/dynmanifest#"
#define NS_OWL "http://www.w3.org/2002/07/owl#"
#define NS_DOAP "http://usefulinc.com/ns/doap#"
#define NS_FOAF "http://xmlns.com/foaf/0.1/"
#define NS_RDF "http://www.w3.org/1999/02/22-rdf-syntax-ns#"
#define NS_RDFS "http://www.w3.org/2000/01/rdf-schema#"
#define NS_XSD "http://www.w3.org/2001/XMLSchema#"

void
lilv_uris_init(LilvURIs* const uris, SordWorld* const world)
{
#define NEW_URI(uri) sord_new_uri(world, (const uint8_t*)(uri))

  uris->atom_supports       = NEW_URI(LV2_ATOM__supports);
  uris->dc_replaces         = NEW_URI(NS_DCTERMS "replaces");
  uris->dman_DynManifest    = NEW_URI(NS_DYNMAN "DynManifest");
  uris->doap_maintainer     = NEW_URI(NS_DOAP "maintainer");
  uris->doap_name           = NEW_URI(NS_DOAP "name");
  uris->event_supportsEvent = NEW_URI(LV2_EVENT__supportsEvent);
  uris->foaf_homepage       = NEW_URI(NS_FOAF "homepage");
  uris->foaf_mbox           = NEW_URI(NS_FOAF "mbox");
  uris->foaf_name           = NEW_URI(NS_FOAF "name");
  uris->lv2_Plugin          = NEW_URI(LV2_CORE__Plugin);
  uris->lv2_Specification   = NEW_URI(LV2_CORE__Specification);
  uris->lv2_appliesTo       = NEW_URI(LV2_CORE__appliesTo);
  uris->lv2_binary          = NEW_URI(LV2_CORE__binary);
  uris->lv2_default         = NEW_URI(LV2_CORE__default);
  uris->lv2_designation     = NEW_URI(LV2_CORE__designation);
  uris->lv2_extensionData   = NEW_URI(LV2_CORE__extensionData);
  uris->lv2_index           = NEW_URI(LV2_CORE__index);
  uris->lv2_latency         = NEW_URI(LV2_CORE__latency);
  uris->lv2_maximum         = NEW_URI(LV2_CORE__maximum);
  uris->lv2_microVersion    = NEW_URI(LV2_CORE__microVersion);
  uris->lv2_minimum         = NEW_URI(LV2_CORE__minimum);
  uris->lv2_minorVersion    = NEW_URI(LV2_CORE__minorVersion);
  uris->lv2_name            = NEW_URI(LV2_CORE__name);
  uris->lv2_optionalFeature = NEW_URI(LV2_CORE__optionalFeature);
  uris->lv2_port            = NEW_URI(LV2_CORE__port);
  uris->lv2_portProperty    = NEW_URI(LV2_CORE__portProperty);
  uris->lv2_project         = NEW_URI(LV2_CORE__project);
  uris->lv2_prototype       = NEW_URI(LV2_CORE__prototype);
  uris->lv2_reportsLatency  = NEW_URI(LV2_CORE__reportsLatency);
  uris->lv2_requiredFeature = NEW_URI(LV2_CORE__requiredFeature);
  uris->lv2_scalePoint      = NEW_URI(LV2_CORE__scalePoint);
  uris->lv2_symbol          = NEW_URI(LV2_CORE__symbol);
  uris->owl_Ontology        = NEW_URI(NS_OWL "Ontology");
  uris->pset_Preset         = NEW_URI(LV2_PRESETS__Preset);
  uris->pset_value          = NEW_URI(LV2_PRESETS__value);
  uris->rdf_type            = NEW_URI(NS_RDF "type");
  uris->rdf_value           = NEW_URI(NS_RDF "value");
  uris->rdfs_Class          = NEW_URI(NS_RDFS "Class");
  uris->rdfs_label          = NEW_URI(NS_RDFS "label");
  uris->rdfs_seeAlso        = NEW_URI(NS_RDFS "seeAlso");
  uris->rdfs_subClassOf     = NEW_URI(NS_RDFS "subClassOf");
  uris->state_state         = NEW_URI(LV2_STATE__state);
  uris->ui_binary           = NEW_URI(LV2_UI__binary);
  uris->ui_ui               = NEW_URI(LV2_UI__ui);
  uris->xsd_base64Binary    = NEW_URI(NS_XSD "base64Binary");
  uris->xsd_boolean         = NEW_URI(NS_XSD "boolean");
  uris->xsd_decimal         = NEW_URI(NS_XSD "decimal");
  uris->xsd_double          = NEW_URI(NS_XSD "double");
  uris->xsd_float           = NEW_URI(NS_XSD "float");
  uris->xsd_integer         = NEW_URI(NS_XSD "integer");
  uris->terminator          = NULL;
}

void
lilv_uris_cleanup(LilvURIs* const uris, SordWorld* const world)
{
  for (SordNode** n = (SordNode**)uris; *n; ++n) {
    sord_node_free(world, *n);
  }
}

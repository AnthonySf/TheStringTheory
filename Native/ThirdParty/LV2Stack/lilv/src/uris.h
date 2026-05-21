// Copyright 2012-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_URIS_H
#define LILV_URIS_H

#include <sord/sord.h>
#include <zix/attributes.h>

typedef struct {
  SordNode* ZIX_ALLOCATED atom_supports;
  SordNode* ZIX_ALLOCATED dc_replaces;
  SordNode* ZIX_ALLOCATED dman_DynManifest;
  SordNode* ZIX_ALLOCATED doap_maintainer;
  SordNode* ZIX_ALLOCATED doap_name;
  SordNode* ZIX_ALLOCATED event_supportsEvent;
  SordNode* ZIX_ALLOCATED foaf_homepage;
  SordNode* ZIX_ALLOCATED foaf_mbox;
  SordNode* ZIX_ALLOCATED foaf_name;
  SordNode* ZIX_ALLOCATED lv2_Plugin;
  SordNode* ZIX_ALLOCATED lv2_Specification;
  SordNode* ZIX_ALLOCATED lv2_appliesTo;
  SordNode* ZIX_ALLOCATED lv2_binary;
  SordNode* ZIX_ALLOCATED lv2_default;
  SordNode* ZIX_ALLOCATED lv2_designation;
  SordNode* ZIX_ALLOCATED lv2_extensionData;
  SordNode* ZIX_ALLOCATED lv2_index;
  SordNode* ZIX_ALLOCATED lv2_latency;
  SordNode* ZIX_ALLOCATED lv2_maximum;
  SordNode* ZIX_ALLOCATED lv2_microVersion;
  SordNode* ZIX_ALLOCATED lv2_minimum;
  SordNode* ZIX_ALLOCATED lv2_minorVersion;
  SordNode* ZIX_ALLOCATED lv2_name;
  SordNode* ZIX_ALLOCATED lv2_optionalFeature;
  SordNode* ZIX_ALLOCATED lv2_port;
  SordNode* ZIX_ALLOCATED lv2_portProperty;
  SordNode* ZIX_ALLOCATED lv2_project;
  SordNode* ZIX_ALLOCATED lv2_prototype;
  SordNode* ZIX_ALLOCATED lv2_reportsLatency;
  SordNode* ZIX_ALLOCATED lv2_requiredFeature;
  SordNode* ZIX_ALLOCATED lv2_scalePoint;
  SordNode* ZIX_ALLOCATED lv2_symbol;
  SordNode* ZIX_ALLOCATED owl_Ontology;
  SordNode* ZIX_ALLOCATED pset_Preset;
  SordNode* ZIX_ALLOCATED pset_value;
  SordNode* ZIX_ALLOCATED rdf_type;
  SordNode* ZIX_ALLOCATED rdf_value;
  SordNode* ZIX_ALLOCATED rdfs_Class;
  SordNode* ZIX_ALLOCATED rdfs_label;
  SordNode* ZIX_ALLOCATED rdfs_seeAlso;
  SordNode* ZIX_ALLOCATED rdfs_subClassOf;
  SordNode* ZIX_ALLOCATED state_state;
  SordNode* ZIX_ALLOCATED ui_binary;
  SordNode* ZIX_ALLOCATED ui_ui;
  SordNode* ZIX_ALLOCATED xsd_base64Binary;
  SordNode* ZIX_ALLOCATED xsd_boolean;
  SordNode* ZIX_ALLOCATED xsd_decimal;
  SordNode* ZIX_ALLOCATED xsd_double;
  SordNode* ZIX_ALLOCATED xsd_float;
  SordNode* ZIX_ALLOCATED xsd_integer;

  SordNode* ZIX_ALLOCATED terminator;
} LilvURIs;

void
lilv_uris_init(LilvURIs* ZIX_NONNULL uris, SordWorld* ZIX_NONNULL world);

void
lilv_uris_cleanup(LilvURIs* ZIX_NONNULL uris, SordWorld* ZIX_NONNULL world);

#endif /* LILV_URIS_H */

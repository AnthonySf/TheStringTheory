// Copyright 2012-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "forge_test_object.h"
#include "test_uri_map.h"

#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/midi/midi.h>

#include <stdbool.h>
#include <stdint.h>
#include <string.h>

#define NS_EG "http://example.org/"

void
forge_test_object(LV2_Atom_Forge* const forge,
                  LV2_URID_Map* const   map,
                  Uris* const           uris,
                  const LV2_URID        obj_id)
{
  const LV2_URID eg_Object  = urid_map(uris, NS_EG "Object");
  const LV2_URID eg_one     = urid_map(uris, NS_EG "aa-one");
  const LV2_URID eg_two     = urid_map(uris, NS_EG "ab-two");
  const LV2_URID eg_three   = urid_map(uris, NS_EG "ac-three");
  const LV2_URID eg_four    = urid_map(uris, NS_EG "ad-four");
  const LV2_URID eg_true    = urid_map(uris, NS_EG "ae-true");
  const LV2_URID eg_false   = urid_map(uris, NS_EG "af-false");
  const LV2_URID eg_path    = urid_map(uris, NS_EG "ag-path");
  const LV2_URID eg_winpath = urid_map(uris, NS_EG "ah-winpath");
  const LV2_URID eg_relpath = urid_map(uris, NS_EG "ai-relpath");
  const LV2_URID eg_urid    = urid_map(uris, NS_EG "aj-urid");
  const LV2_URID eg_string  = urid_map(uris, NS_EG "ak-string");
  const LV2_URID eg_langlit = urid_map(uris, NS_EG "al-langlit");
  const LV2_URID eg_typelit = urid_map(uris, NS_EG "am-typelit");
  const LV2_URID eg_null    = urid_map(uris, NS_EG "an-null");
  const LV2_URID eg_chunk   = urid_map(uris, NS_EG "ba-chunk");
  const LV2_URID eg_blob    = urid_map(uris, NS_EG "bb-blob");
  const LV2_URID eg_blank   = urid_map(uris, NS_EG "bc-blank");
  const LV2_URID eg_tuple   = urid_map(uris, NS_EG "bd-tuple");
  const LV2_URID eg_rectup  = urid_map(uris, NS_EG "be-rectup");
  const LV2_URID eg_ivector = urid_map(uris, NS_EG "bf-ivector");
  const LV2_URID eg_lvector = urid_map(uris, NS_EG "bg-lvector");
  const LV2_URID eg_fvector = urid_map(uris, NS_EG "bh-fvector");
  const LV2_URID eg_dvector = urid_map(uris, NS_EG "bi-dvector");
  const LV2_URID eg_bvector = urid_map(uris, NS_EG "bj-bvector");
  const LV2_URID eg_uvector = urid_map(uris, NS_EG "bj-uvector");
  const LV2_URID eg_fseq    = urid_map(uris, NS_EG "bk-fseq");
  const LV2_URID eg_bseq    = urid_map(uris, NS_EG "bl-bseq");

  LV2_Atom_Forge_Frame obj_frame;
  lv2_atom_forge_object(forge, &obj_frame, obj_id, eg_Object);

  // eg_one = (Int32)1
  lv2_atom_forge_key(forge, eg_one);
  lv2_atom_forge_int(forge, 1);

  // eg_two = (Int64)2
  lv2_atom_forge_key(forge, eg_two);
  lv2_atom_forge_long(forge, 2);

  // eg_three = (Float)3.0
  lv2_atom_forge_key(forge, eg_three);
  lv2_atom_forge_float(forge, 3.0f);

  // eg_four = (Double)4.0
  lv2_atom_forge_key(forge, eg_four);
  lv2_atom_forge_double(forge, 4.0);

  // eg_true = (Bool)1
  lv2_atom_forge_key(forge, eg_true);
  lv2_atom_forge_bool(forge, true);

  // eg_false = (Bool)0
  lv2_atom_forge_key(forge, eg_false);
  lv2_atom_forge_bool(forge, false);

  // eg_path = (Path)"/absolute/path"
  const char*  pstr     = "/absolute/path";
  const size_t pstr_len = strlen(pstr);
  lv2_atom_forge_key(forge, eg_path);
  lv2_atom_forge_path(forge, pstr, pstr_len);

  // eg_winpath = (Path)"C:\Stupid\File System"
  const char*  wpstr     = "C:/Stupid/File System";
  const size_t wpstr_len = strlen(wpstr);
  lv2_atom_forge_key(forge, eg_winpath);
  lv2_atom_forge_path(forge, wpstr, wpstr_len);

  // eg_relpath = (Path)"foo/bar"
  const char*  rpstr     = "foo/bar";
  const size_t rpstr_len = strlen(rpstr);
  lv2_atom_forge_key(forge, eg_relpath);
  lv2_atom_forge_path(forge, rpstr, rpstr_len);

  // eg_urid = (URID)"http://example.org/value"
  LV2_URID eg_value = urid_map(uris, "http://example.org/value");
  lv2_atom_forge_key(forge, eg_urid);
  lv2_atom_forge_urid(forge, eg_value);

  // eg_string = (String)"hello"
  lv2_atom_forge_key(forge, eg_string);
  lv2_atom_forge_string(forge, "hello", strlen("hello"));

  // eg_langlit = (Literal)"ni hao"@cmn (but in actual mandarin)
  const uint8_t ni_hao[] = {0xE4, 0xBD, 0xA0, 0xE5, 0xA5, 0xBD};
  lv2_atom_forge_key(forge, eg_langlit);
  lv2_atom_forge_literal(forge,
                         (const char*)ni_hao,
                         6,
                         0,
                         urid_map(uris, "http://lexvo.org/id/iso639-3/cmn"));

  // eg_typelit = (Literal)"value"^^<http://example.org/Type>
  lv2_atom_forge_key(forge, eg_typelit);
  lv2_atom_forge_literal(forge,
                         "value",
                         strlen("value"),
                         urid_map(uris, "http://example.org/Type"),
                         0);

  // eg_null = null
  lv2_atom_forge_key(forge, eg_null);
  lv2_atom_forge_atom(forge, 0, 0);

  // eg_chunk = 0xBEEFDEAD
  uint8_t chunk_buf[] = {0xBE, 0xEF, 0xDE, 0xAD};
  lv2_atom_forge_key(forge, eg_chunk);
  lv2_atom_forge_atom(forge, sizeof(chunk_buf), forge->Chunk);
  lv2_atom_forge_write(forge, chunk_buf, sizeof(chunk_buf));

  // eg_blob = 0xDEADBEEF
  uint32_t blob_type  = map->map(map->handle, "http://example.org/Blob");
  uint8_t  blob_buf[] = {0xDE, 0xAD, 0xBE, 0xEF};
  lv2_atom_forge_key(forge, eg_blob);
  lv2_atom_forge_atom(forge, sizeof(blob_buf), blob_type);
  lv2_atom_forge_write(forge, blob_buf, sizeof(blob_buf));

  // eg_blank = [ a eg:Object ; blank [ a eg:Object] ]
  lv2_atom_forge_key(forge, eg_blank);
  LV2_Atom_Forge_Frame blank_frame;
  lv2_atom_forge_object(forge, &blank_frame, 0, eg_Object);
  lv2_atom_forge_key(forge, eg_blank);
  LV2_Atom_Forge_Frame sub_blank_frame;
  lv2_atom_forge_object(forge, &sub_blank_frame, 0, eg_Object);
  lv2_atom_forge_pop(forge, &sub_blank_frame);
  lv2_atom_forge_pop(forge, &blank_frame);

  // eg_tuple = "foo",true
  lv2_atom_forge_key(forge, eg_tuple);
  LV2_Atom_Forge_Frame tuple_frame;
  lv2_atom_forge_tuple(forge, &tuple_frame);
  lv2_atom_forge_string(forge, "foo", strlen("foo"));
  lv2_atom_forge_bool(forge, true);
  lv2_atom_forge_pop(forge, &tuple_frame);

  // eg_rectup = "foo",true,("bar",false)
  lv2_atom_forge_key(forge, eg_rectup);
  LV2_Atom_Forge_Frame rectup_frame;
  lv2_atom_forge_tuple(forge, &rectup_frame);
  lv2_atom_forge_string(forge, "foo", strlen("foo"));
  lv2_atom_forge_bool(forge, true);
  LV2_Atom_Forge_Frame subrectup_frame;
  lv2_atom_forge_tuple(forge, &subrectup_frame);
  lv2_atom_forge_string(forge, "bar", strlen("bar"));
  lv2_atom_forge_bool(forge, false);
  lv2_atom_forge_pop(forge, &subrectup_frame);
  lv2_atom_forge_pop(forge, &rectup_frame);

  // eg_ivector = (Vector<Int>)1,2,3,4,5
  lv2_atom_forge_key(forge, eg_ivector);
  const int32_t ielems[] = {1, 2, 3, 4, 5};
  lv2_atom_forge_vector(forge, sizeof(int32_t), forge->Int, 5, ielems);

  // eg_lvector = (Vector<Long>)1,2,3,4
  lv2_atom_forge_key(forge, eg_lvector);
  const int64_t lelems[] = {1, 2, 3, 4};
  lv2_atom_forge_vector(forge, sizeof(int64_t), forge->Long, 4, lelems);

  // eg_fvector = (Vector<Float>)1.0,2.0,3.0,4.0,5.0
  lv2_atom_forge_key(forge, eg_fvector);
  const float felems[] = {1, 2, 3, 4, 5};
  lv2_atom_forge_vector(forge, sizeof(float), forge->Float, 5, felems);

  // eg_dvector = (Vector<Double>)1.0,2.0,3.0,4.0
  lv2_atom_forge_key(forge, eg_dvector);
  const double delems[] = {1, 2, 3, 4};
  lv2_atom_forge_vector(forge, sizeof(double), forge->Double, 4, delems);

  // eg_bvector = (Vector<Bool>)1,0,1
  lv2_atom_forge_key(forge, eg_bvector);
  const int32_t belems[] = {true, false, true};
  lv2_atom_forge_vector(forge, sizeof(int32_t), forge->Bool, 3, belems);

  // eg_uvector = (Vector<URID>)eg:one,eg:two
  lv2_atom_forge_key(forge, eg_uvector);
  const LV2_URID uelems[] = {eg_one, eg_two};
  lv2_atom_forge_vector(forge, sizeof(LV2_URID), forge->URID, 2, uelems);

  // eg_fseq = (Sequence)1, 2
  LV2_URID midi_MidiEvent = map->map(map->handle, LV2_MIDI__MidiEvent);
  lv2_atom_forge_key(forge, eg_fseq);
  LV2_Atom_Forge_Frame fseq_frame;
  lv2_atom_forge_sequence_head(forge, &fseq_frame, 0);

  const uint8_t ev1[3] = {0x90, 0x1A, 0x1};
  lv2_atom_forge_frame_time(forge, 1);
  lv2_atom_forge_atom(forge, sizeof(ev1), midi_MidiEvent);
  lv2_atom_forge_raw(forge, ev1, sizeof(ev1));
  lv2_atom_forge_pad(forge, sizeof(ev1));

  const uint8_t ev2[3] = {0x90, 0x2B, 0x2};
  lv2_atom_forge_frame_time(forge, 3);
  lv2_atom_forge_atom(forge, sizeof(ev2), midi_MidiEvent);
  lv2_atom_forge_raw(forge, ev2, sizeof(ev2));
  lv2_atom_forge_pad(forge, sizeof(ev2));

  lv2_atom_forge_pop(forge, &fseq_frame);

  // eg_bseq = (Sequence)1.1, 2.2
  LV2_URID atom_beatTime = map->map(map->handle, LV2_ATOM__beatTime);
  lv2_atom_forge_key(forge, eg_bseq);
  LV2_Atom_Forge_Frame bseq_frame;
  lv2_atom_forge_sequence_head(forge, &bseq_frame, atom_beatTime);

  lv2_atom_forge_beat_time(forge, 1.0);
  lv2_atom_forge_atom(forge, sizeof(ev1), midi_MidiEvent);
  lv2_atom_forge_raw(forge, ev1, sizeof(ev1));
  lv2_atom_forge_pad(forge, sizeof(ev1));

  lv2_atom_forge_beat_time(forge, 2.0);
  lv2_atom_forge_atom(forge, sizeof(ev2), midi_MidiEvent);
  lv2_atom_forge_raw(forge, ev2, sizeof(ev2));
  lv2_atom_forge_pad(forge, sizeof(ev2));

  lv2_atom_forge_pop(forge, &bseq_frame);

  lv2_atom_forge_pop(forge, &obj_frame);
}

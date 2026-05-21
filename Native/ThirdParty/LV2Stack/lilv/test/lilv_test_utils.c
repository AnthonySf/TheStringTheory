// Copyright 2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_test_utils.h"

#include <lilv/lilv.h>
#include <serd/serd.h>
#include <zix/allocator.h>
#include <zix/filesystem.h>
#include <zix/path.h>
#include <zix/status.h>

#include <assert.h>
#include <errno.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

LilvTestEnv*
lilv_test_env_new(void)
{
  LilvWorld* world = lilv_world_new();
  if (!world) {
    return NULL;
  }

  LilvTestEnv* env = (LilvTestEnv*)calloc(1, sizeof(LilvTestEnv));
  assert(env);

  env->world       = world;
  env->plugin1_uri = lilv_new_uri(world, "http://example.org/plug");
  env->plugin2_uri = lilv_new_uri(world, "http://example.org/foobar");

  // Set custom LV2_PATH in build directory to only use test data
  char*     test_path = zix_canonical_path(NULL, LILV_TEST_DIR);
  char*     lv2_path  = zix_path_join(NULL, test_path, "lv2");
  LilvNode* path      = lilv_new_string(world, lv2_path);
  lilv_world_set_option(world, LILV_OPTION_LV2_PATH, path);
  lilv_node_free(path);
  zix_free(NULL, lv2_path);
  zix_free(NULL, test_path);

  return env;
}

void
lilv_test_env_free(LilvTestEnv* env)
{
  zix_free(NULL, env->test_content_path);
  zix_free(NULL, env->test_manifest_path);
  lilv_node_free(env->test_bundle_uri);
  zix_free(NULL, env->test_bundle_path);
  lilv_node_free(env->plugin2_uri);
  lilv_node_free(env->plugin1_uri);
  lilv_world_free(env->world);
  free(env);
}

int
create_bundle(LilvTestEnv* env,
              const char*  name,
              const char*  manifest,
              const char*  plugin)
{
  {
    char* const test_dir   = zix_canonical_path(NULL, LILV_TEST_DIR);
    char* const bundle_dir = zix_path_join(NULL, test_dir, name);

    env->test_bundle_path = zix_path_join(NULL, bundle_dir, "");

    zix_free(NULL, bundle_dir);
    zix_free(NULL, test_dir);
  }

  if (zix_create_directories(NULL, env->test_bundle_path)) {
    fprintf(stderr,
            "Failed to create directory '%s' (%s)\n",
            env->test_bundle_path,
            strerror(errno));
    return 1;
  }

  SerdNode s = serd_node_new_file_uri(
    (const uint8_t*)env->test_bundle_path, NULL, NULL, true);

  env->test_bundle_uri = lilv_new_uri(env->world, (const char*)s.buf);

  env->test_manifest_path =
    zix_path_join(NULL, env->test_bundle_path, "manifest.ttl");

  env->test_content_path =
    zix_path_join(NULL, env->test_bundle_path, "plugin.ttl");

  serd_node_free(&s);

  FILE* const manifest_file = fopen(env->test_manifest_path, "w");
  if (!manifest_file) {
    return 2;
  }

  FILE* const plugin_file = fopen(env->test_content_path, "w");
  if (!plugin_file) {
    fclose(manifest_file);
    return 3;
  }

  const size_t manifest_head_len = strlen(MANIFEST_PREFIXES);
  const size_t manifest_len      = strlen(manifest);
  const size_t plugin_head_len   = strlen(PLUGIN_PREFIXES);
  const size_t plugin_len        = strlen(plugin);
  const size_t n_total =
    manifest_len + plugin_len + manifest_head_len + plugin_head_len;

  size_t n_written = 0;
  n_written += fwrite(MANIFEST_PREFIXES, 1, manifest_head_len, manifest_file);
  n_written += fwrite(manifest, 1, manifest_len, manifest_file);
  n_written += fwrite(PLUGIN_PREFIXES, 1, plugin_head_len, plugin_file);
  n_written += fwrite(plugin, 1, plugin_len, plugin_file);

  fclose(manifest_file);
  fclose(plugin_file);
  return n_written == n_total ? 0 : 4;
}

int
start_bundle(LilvTestEnv* env,
             const char*  name,
             const char*  manifest,
             const char*  plugin)
{
  if (create_bundle(env, name, manifest, plugin)) {
    return 1;
  }

  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  return 0;
}

static void
remove_temporary(const char* const path)
{
  const ZixStatus st = zix_remove(path);
  if (st) {
    fprintf(stderr, "Failed to remove '%s' (%s)\n", path, zix_strerror(st));
  }
}

void
delete_bundle(LilvTestEnv* env)
{
  if (env->test_content_path) {
    remove_temporary(env->test_content_path);
  }

  if (env->test_manifest_path) {
    remove_temporary(env->test_manifest_path);
  }

  if (env->test_bundle_path) {
    remove_temporary(env->test_bundle_path);
  }

  zix_free(NULL, env->test_content_path);
  zix_free(NULL, env->test_manifest_path);
  lilv_node_free(env->test_bundle_uri);
  zix_free(NULL, env->test_bundle_path);

  env->test_content_path  = NULL;
  env->test_manifest_path = NULL;
  env->test_bundle_uri    = NULL;
  env->test_bundle_path   = NULL;
}

void
set_env(const char* name, const char* value)
{
#ifdef _WIN32
  // setenv on Windows does not modify the current process' environment
  const size_t len = strlen(name) + 1 + strlen(value) + 1;
  char*        str = (char*)calloc(1, len);
  assert(str);
  snprintf(str, len, "%s=%s", name, value);
  putenv(str);
  free(str);
#else
  setenv(name, value, 1);
#endif
}

char*
lilv_create_temporary_directory(const char* pattern)
{
  char* const tmpdir       = zix_temp_directory_path(NULL);
  char* const path_pattern = zix_path_join(NULL, tmpdir, pattern);
  char* const result       = zix_create_temporary_directory(NULL, path_pattern);

  zix_free(NULL, path_pattern);
  zix_free(NULL, tmpdir);

  return result;
}

char*
string_concat(const char* const head, const char* const tail)
{
  const size_t head_len = strlen(head);
  const size_t tail_len = strlen(tail);
  char* const  result   = (char*)calloc(1U, head_len + tail_len + 1U);
  if (result) {
    memcpy(result, head, head_len + 1U);
    memcpy(result + head_len, tail, tail_len + 1U);
  }
  return result;
}

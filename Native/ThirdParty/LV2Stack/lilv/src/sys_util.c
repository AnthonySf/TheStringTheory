// Copyright 2007-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "sys_util.h"
#include "log.h"
#include "string_util.h"

#include <zix/allocator.h>
#include <zix/filesystem.h>
#include <zix/path.h>
#include <zix/string_view.h>

#include <sys/stat.h>

#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

char*
lilv_get_lang(void)
{
  const char* const env_lang = getenv("LANG");
  if (!env_lang || !strcmp(env_lang, "") || !strcmp(env_lang, "C") ||
      !strcmp(env_lang, "POSIX")) {
    return NULL;
  }

  return lilv_normalize_lang(env_lang);
}

char*
lilv_normalize_lang(const char* const env_lang)
{
  const size_t env_lang_len = strlen(env_lang);
  char* const  lang         = (char*)malloc(env_lang_len + 1);
  for (size_t i = 0; i < env_lang_len + 1; ++i) {
    if (env_lang[i] == '_') {
      lang[i] = '-'; // Convert _ to -
    } else if (env_lang[i] >= 'A' && env_lang[i] <= 'Z') {
      lang[i] = env_lang[i] + ('a' - 'A'); // Convert to lowercase
    } else if ((env_lang[i] >= 'a' && env_lang[i] <= 'z') ||
               (env_lang[i] >= '0' && env_lang[i] <= '9')) {
      lang[i] = env_lang[i]; // Lowercase letter or digit, copy verbatim
    } else if (env_lang[i] == '\0' || env_lang[i] == '.') {
      // End, or start of suffix (e.g. en_CA.utf-8), finished
      lang[i] = '\0';
      break;
    } else {
      LILV_ERRORF("Illegal LANG \"%s\" ignored\n", env_lang);
      free(lang);
      return NULL;
    }
  }

  return lang;
}

char*
lilv_find_path(const char*             in_path,
               const LilvPathCheckFunc predicate,
               const void*             user_data)
{
  const size_t in_path_len = strlen(in_path);
  char*        path        = (char*)malloc(in_path_len + 7);
  memcpy(path, in_path, in_path_len + 1);

  for (unsigned i = 2U; i < 1000000U; ++i) {
    if (predicate(path, user_data) != LILV_PATH_IS_TAKEN) {
      return path;
    }
    snprintf(path, in_path_len + 7, "%s.%u", in_path, i);
  }

  return NULL;
}

typedef struct {
  char*  pattern;
  time_t time;
  char*  latest;
} Latest;

static void
update_latest(const char* path, const char* name, void* data)
{
  Latest*  latest     = (Latest*)data;
  char*    entry_path = zix_path_join(NULL, path, name);
  unsigned num        = 0;
  if (sscanf(entry_path, latest->pattern, &num) == 1) {
    struct stat st;
    if (!stat(entry_path, &st)) {
      if (st.st_mtime >= latest->time) {
        zix_free(NULL, latest->latest);
        latest->latest = entry_path;
      }
    } else {
      LILV_ERRORF("stat(%s) (%s)\n", path, strerror(errno));
    }
  }
  if (entry_path != latest->latest) {
    zix_free(NULL, entry_path);
  }
}

char*
lilv_get_latest_copy(const char* path, const char* copy_path)
{
  char* copy_dir = zix_string_view_copy(NULL, zix_path_parent_path(copy_path));

  Latest latest = {lilv_strjoin(copy_path, ".%u", NULL), 0, NULL};

  struct stat st;
  if (!stat(path, &st)) {
    latest.time = st.st_mtime;
  } else {
    LILV_ERRORF("stat(%s) (%s)\n", path, strerror(errno));
  }

  zix_dir_for_each(copy_dir, &latest, update_latest);

  free(latest.pattern);
  zix_free(NULL, copy_dir);
  return latest.latest;
}

// Copyright 2020-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/filesystem.h>
#include <zix/path.h>
#include <zix/status.h>
#include <zix/string_view.h>

#ifndef _WIN32
#  include <unistd.h>
#endif

#if defined(_POSIX_VERSION) && _POSIX_VERSION >= 200112L
#  include <sys/socket.h>
#  include <sys/stat.h>
#  include <sys/un.h>
#endif

#include <assert.h>
#include <errno.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void
test_temp_directory_path(void)
{
  char* tmpdir = zix_temp_directory_path(NULL);

  assert(zix_file_type(tmpdir) == ZIX_FILE_TYPE_DIRECTORY);

  free(tmpdir);
}

static void
test_current_path(void)
{
  char* cwd = zix_current_path(NULL);

  assert(zix_file_type(cwd) == ZIX_FILE_TYPE_DIRECTORY);

  free(cwd);
}

static char*
create_temp_dir(const char* const name_pattern)
{
  char* const temp = zix_temp_directory_path(NULL);
  assert(temp);

  char* const path_pattern = zix_path_join(NULL, temp, name_pattern);
  char* const result       = zix_create_temporary_directory(NULL, path_pattern);
  free(path_pattern);
  zix_free(NULL, temp);
  assert(result);
  return result;
}

static int
write_to_path(const char* const path, const char* const contents)
{
  int         ret = -1;
  FILE* const f   = fopen(path, "wb");
  if (f) {
    fwrite(contents, 1, strlen(contents), f);
    ret = fflush(f) ? errno : ferror(f) ? EBADF : 0;
    ret = (fclose(f) && !ret) ? errno : ret;
  }

  return ret;
}

static void
test_canonical_path(void)
{
  assert(!zix_canonical_path(NULL, NULL));

  char* const temp_dir = create_temp_dir("zixXXXXXX");
  char* const sub_dir  = zix_path_join(NULL, temp_dir, "sub");
  assert(!zix_create_directory(sub_dir));
  assert(zix_file_type(temp_dir) == ZIX_FILE_TYPE_DIRECTORY);

  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(file_path);
  assert(!write_to_path(file_path, "test\n"));

#ifndef _WIN32
  // Test symlink resolution

  char* const link_path = zix_path_join(NULL, temp_dir, "zix_test_link");

  assert(!zix_create_symlink(file_path, link_path));

  char* const real_file_path = zix_canonical_path(NULL, file_path);
  char* const real_link_path = zix_canonical_path(NULL, link_path);

  assert(real_file_path);
  assert(real_link_path);
  assert(!strcmp(real_file_path, real_link_path));

  assert(!zix_remove(link_path));
  free(real_link_path);
  free(real_file_path);
  free(link_path);
#endif

  // Test dot segment resolution

  char* const parent_dir_1 = zix_path_join(NULL, sub_dir, "..");
  assert(parent_dir_1);

  const ZixStringView parent_view  = zix_path_parent_path(sub_dir);
  char* const         parent_dir_2 = zix_string_view_copy(NULL, parent_view);
  assert(parent_dir_2);
  assert(parent_dir_2[0]);

  char* const real_parent_dir_1 = zix_canonical_path(NULL, parent_dir_1);
  char* const real_parent_dir_2 = zix_canonical_path(NULL, parent_dir_2);

  assert(real_parent_dir_1);
  assert(real_parent_dir_2);
  assert(!strcmp(real_parent_dir_1, real_parent_dir_2));

  // Test missing files

  assert(!zix_canonical_path(NULL, "/presumuably/absent"));
  assert(!zix_canonical_path(NULL, "/presumuably/absent/"));
  assert(!zix_canonical_path(NULL, "presumuably_absent"));

  // Clean everything up

  assert(!zix_remove(file_path));
  assert(!zix_remove(sub_dir));
  assert(!zix_remove(temp_dir));

  free(real_parent_dir_2);
  free(real_parent_dir_1);
  free(parent_dir_2);
  free(parent_dir_1);
  free(file_path);
  free(sub_dir);
  free(temp_dir);
}

static void
test_file_type(void)
{
  char* const temp_dir  = create_temp_dir("zixXXXXXX");
  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(file_path);
  assert(zix_file_type(file_path) == ZIX_FILE_TYPE_NONE);

  // Directory
  assert(zix_file_type(temp_dir) == ZIX_FILE_TYPE_DIRECTORY);

  // Regular file
  assert(!write_to_path(file_path, "test\n"));
  assert(zix_file_type(file_path) == ZIX_FILE_TYPE_REGULAR);
  assert(!zix_remove(file_path));

#if defined(_POSIX_VERSION) && _POSIX_VERSION >= 200112L

  // FIFO
  if (!mkfifo(file_path, 0600)) {
    assert(zix_file_type(file_path) == ZIX_FILE_TYPE_FIFO);
    assert(!zix_remove(file_path));
  }

  // Socket
  const int sock = socket(AF_UNIX, SOCK_STREAM, 0);
  if (sock >= 0) {
    const socklen_t           addr_len = sizeof(struct sockaddr_un);
    struct sockaddr_un* const addr = (struct sockaddr_un*)calloc(1, addr_len);
    assert(addr);

    if (strlen(file_path) < sizeof(addr->sun_path)) {
      addr->sun_family = AF_UNIX;
      strncpy(addr->sun_path, file_path, sizeof(addr->sun_path) - 1);

      const int fd = bind(sock, (struct sockaddr*)addr, addr_len);
      if (fd >= 0) {
        assert(zix_file_type(file_path) == ZIX_FILE_TYPE_SOCKET);
        assert(!zix_remove(file_path));
        close(fd);
      }
    } // otherwise, TMPDIR is oddly long, skip test

    close(sock);
    free(addr);
  }

#endif

  assert(!zix_remove(temp_dir));
  free(file_path);
  free(temp_dir);
}

static void
test_copy_file(const char* data_file_path)
{
  char* const temp_dir      = create_temp_dir("zixXXXXXX");
  char* const tmp_file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  char* const copy_path     = zix_path_join(NULL, temp_dir, "zix_test_copy");
  assert(tmp_file_path);
  assert(copy_path);

  data_file_path = data_file_path ? data_file_path : tmp_file_path;

  assert(!write_to_path(tmp_file_path, "test\n"));

  // Fail to copy to/from a nonexistent file
  assert(zix_copy_file(NULL, tmp_file_path, "/does/not/exist", 0U));
  assert(zix_copy_file(NULL, "/does/not/exist", copy_path, 0U));
  assert(zix_file_type(copy_path) == ZIX_FILE_TYPE_NONE);

  // Fail to copy from/to a directory
  assert(zix_copy_file(NULL, temp_dir, copy_path, 0U));
  assert(zix_copy_file(NULL, tmp_file_path, temp_dir, 0U));

  if (data_file_path) {
    // Fail to copy a file to itself
    assert(zix_copy_file(NULL, data_file_path, data_file_path, 0U) ==
           ZIX_STATUS_EXISTS);

    // Successful copy between filesystems
    assert(!zix_copy_file(NULL, data_file_path, copy_path, 0U));
    assert(zix_file_equals(NULL, data_file_path, copy_path));

    // Trying the same again fails because the copy path already exists
    assert(zix_copy_file(NULL, data_file_path, copy_path, 0U) ==
           ZIX_STATUS_EXISTS);

    // Unless overwriting is requested
    assert(!zix_copy_file(
      NULL, data_file_path, copy_path, ZIX_COPY_OPTION_OVERWRITE_EXISTING));

    assert(!zix_remove(copy_path));
  }

  // Successful copy within a filesystem
  assert(zix_file_type(copy_path) == ZIX_FILE_TYPE_NONE);
  assert(!zix_copy_file(NULL, tmp_file_path, copy_path, 0U));
  assert(zix_file_equals(NULL, tmp_file_path, copy_path));
  assert(!zix_remove(copy_path));

  if (zix_file_type("/dev/random") == ZIX_FILE_TYPE_CHARACTER) {
    // Fail to copy infinite file to a file
    assert(zix_copy_file(NULL, "/dev/random", copy_path, 0U) ==
           ZIX_STATUS_BAD_ARG);

    // Fail to copy infinite file to itself
    assert(zix_copy_file(NULL, "/dev/random", "/dev/random", 0U) ==
           ZIX_STATUS_BAD_ARG);

    // Fail to copy infinite file to another
    assert(zix_copy_file(NULL, "/dev/random", "/dev/urandom", 0U) ==
           ZIX_STATUS_BAD_ARG);
  }

  if (zix_file_type("/dev/full") == ZIX_FILE_TYPE_CHARACTER) {
    if (data_file_path) {
      assert(zix_copy_file(NULL,
                           data_file_path,
                           "/dev/full",
                           ZIX_COPY_OPTION_OVERWRITE_EXISTING) ==
             ZIX_STATUS_NO_SPACE);
    }

    // Copy short file (error after flushing)
    assert(zix_copy_file(
      NULL, tmp_file_path, "/dev/full", ZIX_COPY_OPTION_OVERWRITE_EXISTING));

    // Copy long file (error during writing)
    FILE* const f = fopen(tmp_file_path, "wb");
    assert(f);
    for (size_t i = 0; i < 4096; ++i) {
      fprintf(f, "test\n");
    }
    fclose(f);
    assert(zix_copy_file(
      NULL, tmp_file_path, "/dev/full", ZIX_COPY_OPTION_OVERWRITE_EXISTING));
  }

  assert(!zix_remove(tmp_file_path));
  assert(!zix_remove(temp_dir));

  free(copy_path);
  free(tmp_file_path);
  free(temp_dir);
}

static void
test_flock(void)
{
  char* const temp_dir  = create_temp_dir("zixXXXXXX");
  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(file_path);

  FILE* const f1 = fopen(file_path, "wb");
  FILE* const f2 = fopen(file_path, "a+b");

  assert(f1);
  assert(f2);

  ZixStatus st = zix_file_lock(f1, ZIX_FILE_LOCK_TRY);
  assert(!st || st == ZIX_STATUS_NOT_SUPPORTED);

  if (!st) {
    assert(zix_file_lock(f2, ZIX_FILE_LOCK_TRY) == ZIX_STATUS_UNAVAILABLE);
    assert(!zix_file_unlock(f1, ZIX_FILE_LOCK_TRY));
  }

  fclose(f2);
  fclose(f1);
  assert(!zix_remove(file_path));
  assert(!zix_remove(temp_dir));
  free(file_path);
  free(temp_dir);
}

typedef struct {
  size_t n_names;
  char** names;
} FileList;

static void
visit(const char* const path, const char* const name, void* const data)
{
  (void)path;

  const size_t    name_len  = strlen(name);
  FileList* const file_list = (FileList*)data;

  char** const new_names =
    (char**)realloc(file_list->names, sizeof(char*) * ++file_list->n_names);

  if (new_names) {
    char* const name_copy = (char*)calloc(name_len + 1, 1);
    assert(name_copy);
    memcpy(name_copy, name, name_len + 1);

    file_list->names                         = new_names;
    file_list->names[file_list->n_names - 1] = name_copy;
  }
}

static void
test_dir_for_each(void)
{
  char* const temp_dir = create_temp_dir("zixXXXXXX");
  char* const path1    = zix_path_join(NULL, temp_dir, "zix_test_1");
  char* const path2    = zix_path_join(NULL, temp_dir, "zix_test_2");
  assert(path1);
  assert(path2);

  assert(!write_to_path(path1, "test\n"));
  assert(!write_to_path(path2, "test\n"));

  FileList file_list = {0, NULL};
  zix_dir_for_each(temp_dir, &file_list, visit);

  assert(file_list.names);
  assert((!strcmp(file_list.names[0], "zix_test_1") &&
          !strcmp(file_list.names[1], "zix_test_2")) ||
         (!strcmp(file_list.names[0], "zix_test_2") &&
          !strcmp(file_list.names[1], "zix_test_1")));

  assert(!zix_remove(path2));
  assert(!zix_remove(path1));
  assert(!zix_remove(temp_dir));

  free(file_list.names[0]);
  free(file_list.names[1]);
  free(file_list.names);
  free(path2);
  free(path1);
  free(temp_dir);
}

static void
test_create_temporary_directory(void)
{
  assert(!zix_create_temporary_directory(NULL, ""));

  char* const path1 = create_temp_dir("zixXXXXXX");
  assert(path1);
  assert(zix_file_type(path1) == ZIX_FILE_TYPE_DIRECTORY);

  char* const path2 = create_temp_dir("zixXXXXXX");
  assert(path2);
  assert(strcmp(path1, path2));
  assert(zix_file_type(path2) == ZIX_FILE_TYPE_DIRECTORY);

  assert(!zix_remove(path2));
  assert(!zix_remove(path1));
  free(path2);
  free(path1);
}

static void
test_create_directory_like(void)
{
  char* const temp_dir = create_temp_dir("zixXXXXXX");
  assert(zix_file_type(temp_dir) == ZIX_FILE_TYPE_DIRECTORY);

  char* const sub_dir = zix_path_join(NULL, temp_dir, "sub");
  assert(zix_create_directory_like(sub_dir, sub_dir) == ZIX_STATUS_NOT_FOUND);
  assert(!zix_create_directory_like(sub_dir, temp_dir));
  assert(zix_file_type(sub_dir) == ZIX_FILE_TYPE_DIRECTORY);
  assert(!zix_remove(sub_dir));
  zix_free(NULL, sub_dir);

  assert(!zix_remove(temp_dir));
  free(temp_dir);
}

static void
test_create_directories(void)
{
  char* const temp_dir = create_temp_dir("zixXXXXXX");
  assert(temp_dir);
  assert(zix_file_type(temp_dir) == ZIX_FILE_TYPE_DIRECTORY);

  assert(zix_create_directories(NULL, "") == ZIX_STATUS_BAD_ARG);

  char* const child_dir      = zix_path_join(NULL, temp_dir, "child");
  char* const grandchild_dir = zix_path_join(NULL, child_dir, "grandchild");

  {
    ZixFailingAllocator bad_allocator = zix_failing_allocator();
    zix_failing_allocator_reset(&bad_allocator, 0U);
    assert(zix_create_directories(&bad_allocator.base, grandchild_dir) ==
           ZIX_STATUS_NO_MEM);
  }

  assert(!zix_create_directories(NULL, grandchild_dir));
  assert(zix_file_type(grandchild_dir) == ZIX_FILE_TYPE_DIRECTORY);
  assert(zix_file_type(child_dir) == ZIX_FILE_TYPE_DIRECTORY);

  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(!write_to_path(file_path, "test\n"));

  assert(zix_create_directories(NULL, file_path) == ZIX_STATUS_EXISTS);

  assert(!zix_remove(file_path));
  assert(!zix_remove(grandchild_dir));
  assert(!zix_remove(child_dir));
  assert(!zix_remove(temp_dir));
  free(file_path);
  free(child_dir);
  free(grandchild_dir);
  free(temp_dir);
}

static bool
check_file_equals(const char* const path1, const char* const path2)
{
  const bool r1 = zix_file_equals(NULL, path1, path2);
  const bool r2 = zix_file_equals(NULL, path2, path1);
  assert(r2 == r1);

  ZixFailingAllocator bad_allocator = zix_failing_allocator();
  zix_failing_allocator_reset(&bad_allocator, 0U);

  const bool r3 = zix_file_equals(&bad_allocator.base, path1, path2);
  const bool r4 = zix_file_equals(&bad_allocator.base, path2, path1);
  assert(r3 == r1);
  assert(r4 == r1);

  return r1;
}

static void
test_file_equals(const char* const data_file_path)
{
  char* const temp_dir = create_temp_dir("zixXXXXXX");
  char* const path1    = zix_path_join(NULL, temp_dir, "zix1");
  char* const path2    = zix_path_join(NULL, temp_dir, "zix2");

  // Equal: test, test
  assert(!write_to_path(path1, "test"));
  assert(!write_to_path(path2, "test"));
  assert(check_file_equals(path1, path2));

  // Missing file
  assert(!check_file_equals(path1, "/does/not/exist"));

  // Longer RHS: test, testdiff
  assert(!write_to_path(path2, "diff"));
  assert(!check_file_equals(path1, path2));

  // Longer LHS: testdifflong, testdiff
  assert(!write_to_path(path1, "difflong"));
  assert(!check_file_equals(path1, path2));

  // Equal sizes but different content: testdifflong, testdifflang
  assert(!write_to_path(path2, "difflang"));
  assert(!check_file_equals(path1, path2));

  // Same path
  assert(check_file_equals(path1, path1));

  // Different devices
  if (data_file_path) {
    assert(!check_file_equals(data_file_path, path2));
    assert(!zix_remove(path2));
    assert(!zix_copy_file(NULL, data_file_path, path2, 0U));
    assert(check_file_equals(data_file_path, path2));
  }

  assert(!zix_remove(path2));
  assert(!zix_remove(path1));
  assert(!zix_remove(temp_dir));

  free(path2);
  free(path1);
  free(temp_dir);
}

static void
test_file_size(void)
{
  static const ZixStringView contents = ZIX_STATIC_STRING("file size test");

  char* const temp_dir = create_temp_dir("zixXXXXXX");
  char* const path     = zix_path_join(NULL, temp_dir, "zix_test");

  // Nonexistent files
  assert(!zix_file_size(path));
  assert(!zix_file_size("/does/not/exist"));

  // Empty file
  assert(!write_to_path(path, ""));
  assert(!zix_file_size(path));

  // Non-empty file
  assert(!write_to_path(path, contents.data));
  assert((size_t)zix_file_size(path) == contents.length);

  assert(!zix_remove(path));
  assert(!zix_remove(temp_dir));

  free(path);
  free(temp_dir);
}

static void
test_create_symlink(void)
{
  static const char* const contents = "zixtest";

  char* const temp_dir = create_temp_dir("zixXXXXXX");

  // Write contents to original file
  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(!write_to_path(file_path, contents));

  // Ensure original file exists and is a regular file
  assert(zix_symlink_type(file_path) == ZIX_FILE_TYPE_REGULAR);

  // Create symlink to original file
  char* const     link_path = zix_path_join(NULL, temp_dir, "zix_test_link");
  const ZixStatus st        = zix_create_symlink(file_path, link_path);

  // Check that the symlink seems equivalent to the original file
  assert(!st || st == ZIX_STATUS_NOT_SUPPORTED || st == ZIX_STATUS_BAD_PERMS);
  assert(!st || zix_symlink_type(link_path) == ZIX_FILE_TYPE_NONE);
  if (!st) {
    assert(zix_symlink_type(link_path) == ZIX_FILE_TYPE_SYMLINK);
    assert(zix_file_type(link_path) == ZIX_FILE_TYPE_REGULAR);
    assert(zix_file_equals(NULL, file_path, link_path));
    assert(!zix_remove(link_path));
  }

  assert(!zix_remove(file_path));
  assert(!zix_remove(temp_dir));

  free(link_path);
  free(file_path);
  free(temp_dir);
}

static void
test_create_directory_symlink(void)
{
  char* const     temp_dir  = create_temp_dir("zixXXXXXX");
  char* const     link_path = zix_path_join(NULL, temp_dir, "zix_test_link");
  const ZixStatus st        = zix_create_directory_symlink(temp_dir, link_path);

  if (st != ZIX_STATUS_NOT_SUPPORTED && st != ZIX_STATUS_BAD_PERMS) {
    assert(!st);
    assert(zix_file_type(link_path) == ZIX_FILE_TYPE_DIRECTORY);

#ifdef _WIN32
    assert(zix_symlink_type(link_path) == ZIX_FILE_TYPE_DIRECTORY);
#else
    assert(zix_symlink_type(link_path) == ZIX_FILE_TYPE_SYMLINK);
#endif

    assert(!zix_remove(link_path));
  }

  assert(!zix_remove(temp_dir));
  free(link_path);
  free(temp_dir);
}

static void
test_create_hard_link(void)
{
  static const char* const contents = "zixtest";

  char* const temp_dir = create_temp_dir("zixXXXXXX");

  // Write contents to original file
  char* const file_path = zix_path_join(NULL, temp_dir, "zix_test_file");
  assert(!write_to_path(file_path, contents));

  // Ensure original file exists and is a regular file
  assert(zix_symlink_type(file_path) == ZIX_FILE_TYPE_REGULAR);

  // Create symlink to original file
  char* const     link_path = zix_path_join(NULL, temp_dir, "zix_test_link");
  const ZixStatus st        = zix_create_hard_link(file_path, link_path);

  // Check that the link is equivalent to the original file
  assert(!st || st == ZIX_STATUS_NOT_SUPPORTED || st == ZIX_STATUS_MAX_LINKS);
  assert(!st || zix_symlink_type(link_path) == ZIX_FILE_TYPE_NONE);
  if (!st) {
    assert(zix_file_type(link_path) == ZIX_FILE_TYPE_REGULAR);
    assert(zix_file_equals(NULL, file_path, link_path));
    assert(!zix_remove(link_path));
  }

  assert(!zix_remove(file_path));
  assert(!zix_remove(temp_dir));

  free(link_path);
  free(file_path);
  free(temp_dir);
}

int
main(const int argc, char** const argv)
{
  // Try to find some existing data file that's ideally not on a tmpfs
  const char* const default_file_path = (argc > 1) ? argv[1] : "build.ninja";
  const char* const data_file_path =
    (zix_file_type(default_file_path) == ZIX_FILE_TYPE_REGULAR)
      ? default_file_path
      : NULL;

  test_temp_directory_path();
  test_current_path();
  test_canonical_path();
  test_file_type();
  test_copy_file(data_file_path);
  test_flock();
  test_dir_for_each();
  test_create_temporary_directory();
  test_create_directory_like();
  test_create_directories();
  test_file_equals(data_file_path);
  test_file_size();
  test_create_symlink();
  test_create_directory_symlink();
  test_create_hard_link();

  return 0;
}

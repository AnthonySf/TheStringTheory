// Copyright 2020-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/path.h>
#include <zix/string_view.h>

#include <assert.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

/// Return whether `string` equals `expected`
static bool
equal(char* const string, const char* const expected)
{
  const bool result =
    (!string && !expected) || (string && expected && !strcmp(string, expected));

  free(string);
  return result;
}

/// Return whether `string` matches `expected` with preferred separators
static bool
match(char* const string, const char* const expected)
{
  char* const e = expected ? zix_path_preferred(NULL, expected) : NULL;
  char* const s = string ? zix_path_preferred(NULL, string) : NULL;

  const bool result = (!s && !e) || (s && e && !strcmp(s, e));

  free(s);
  free(e);
  free(string);
  return result;
}

/// Return whether `view` equals `expected`
static bool
view_equal(const ZixStringView view, const char* const expected)
{
  const bool result =
    (!view.length && !expected) ||
    (view.length && expected && strlen(expected) == view.length &&
     !strncmp(view.data, expected, view.length));

  return result;
}

static void
test_path_join(void)
{
  // Trivial cases
  assert(equal(zix_path_join(NULL, "", ""), ""));
  assert(equal(zix_path_join(NULL, "", "/b/"), "/b/"));
  assert(equal(zix_path_join(NULL, "", "b"), "b"));
  assert(equal(zix_path_join(NULL, "/", ""), "/"));
  assert(equal(zix_path_join(NULL, "/a/", ""), "/a/"));
  assert(equal(zix_path_join(NULL, "/a/b/", ""), "/a/b/"));
  assert(equal(zix_path_join(NULL, "a/", ""), "a/"));
  assert(equal(zix_path_join(NULL, "a/b/", ""), "a/b/"));

  // Null is treated like an empty path
  assert(equal(zix_path_join(NULL, "/", NULL), "/"));
  assert(equal(zix_path_join(NULL, "/a/", NULL), "/a/"));
  assert(equal(zix_path_join(NULL, "/a/b/", NULL), "/a/b/"));
  assert(equal(zix_path_join(NULL, "a/", NULL), "a/"));
  assert(equal(zix_path_join(NULL, "a/b/", NULL), "a/b/"));
  assert(equal(zix_path_join(NULL, NULL, "/b"), "/b"));
  assert(equal(zix_path_join(NULL, NULL, "/b/c"), "/b/c"));
  assert(equal(zix_path_join(NULL, NULL, "b"), "b"));
  assert(equal(zix_path_join(NULL, NULL, "b/c"), "b/c"));
  assert(equal(zix_path_join(NULL, NULL, NULL), ""));

  // Joining an absolute path discards the left path
  assert(equal(zix_path_join(NULL, "/a", "/b"), "/b"));
  assert(equal(zix_path_join(NULL, "/a", "/b/"), "/b/"));
  assert(equal(zix_path_join(NULL, "a", "/b"), "/b"));
  assert(equal(zix_path_join(NULL, "a/", "/b"), "/b"));

  // Concatenation cases
  assert(equal(zix_path_join(NULL, "/a/", "b"), "/a/b"));
  assert(equal(zix_path_join(NULL, "/a/", "b/"), "/a/b/"));
  assert(equal(zix_path_join(NULL, "a/", "b"), "a/b"));
  assert(equal(zix_path_join(NULL, "a/", "b/"), "a/b/"));
  assert(equal(zix_path_join(NULL, "/a/c/", "/b/d"), "/b/d"));
  assert(equal(zix_path_join(NULL, "/a/c/", "b"), "/a/c/b"));

#ifdef _WIN32
  // "C:" is a drive letter, "//host" is a share, backslash is a separator
  assert(equal(zix_path_join(NULL, "//host", ""), "//host\\"));
  assert(equal(zix_path_join(NULL, "//host", "a"), "//host\\a"));
  assert(equal(zix_path_join(NULL, "//host/", "a"), "//host/a"));
  assert(equal(zix_path_join(NULL, "/a", ""), "/a\\"));
  assert(equal(zix_path_join(NULL, "/a", "b"), "/a\\b"));
  assert(equal(zix_path_join(NULL, "/a", "b/"), "/a\\b/"));
  assert(equal(zix_path_join(NULL, "/a", NULL), "/a\\"));
  assert(equal(zix_path_join(NULL, "/a/b", NULL), "/a/b\\"));
  assert(equal(zix_path_join(NULL, "/a/c", "b"), "/a/c\\b"));
  assert(equal(zix_path_join(NULL, "C:", ""), "C:"));
  assert(equal(zix_path_join(NULL, "C:/a", "/b"), "C:/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "C:/b"), "C:/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "C:b"), "C:/a\\b"));
  assert(equal(zix_path_join(NULL, "C:/a", "D:b"), "D:b"));
  assert(equal(zix_path_join(NULL, "C:/a", "b"), "C:/a\\b"));
  assert(equal(zix_path_join(NULL, "C:/a", "b/"), "C:/a\\b/"));
  assert(equal(zix_path_join(NULL, "C:/a", "c:/b"), "c:/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "c:b"), "c:b"));
  assert(equal(zix_path_join(NULL, "C:\\a", "b"), "C:\\a\\b"));
  assert(equal(zix_path_join(NULL, "C:\\a", "b\\"), "C:\\a\\b\\"));
  assert(equal(zix_path_join(NULL, "C:a", "/b"), "C:/b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:/b"), "C:/b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:\\b"), "C:\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:b"), "C:a\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "b"), "C:a\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:/b"), "c:/b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:\\b"), "c:\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:b"), "c:b"));
  assert(equal(zix_path_join(NULL, "a", ""), "a\\"));
  assert(equal(zix_path_join(NULL, "a", "C:"), "C:"));
  assert(equal(zix_path_join(NULL, "a", "C:/b"), "C:/b"));
  assert(equal(zix_path_join(NULL, "a", "C:\\b"), "C:\\b"));
  assert(equal(zix_path_join(NULL, "a", "\\b"), "\\b"));
  assert(equal(zix_path_join(NULL, "a", "b"), "a\\b"));
  assert(equal(zix_path_join(NULL, "a", "b/"), "a\\b/"));
  assert(equal(zix_path_join(NULL, "a", NULL), "a\\"));
  assert(equal(zix_path_join(NULL, "a/b", ""), "a/b\\"));
  assert(equal(zix_path_join(NULL, "a/b", NULL), "a/b\\"));
  assert(equal(zix_path_join(NULL, "a\\", "\\b"), "\\b"));
  assert(equal(zix_path_join(NULL, "a\\", "b"), "a\\b"));
#else
  // "C:" isn't special, "//host" has extra slashes, slash is the only separator
  assert(equal(zix_path_join(NULL, "//host", ""), "//host/"));
  assert(equal(zix_path_join(NULL, "//host", "a"), "//host/a"));
  assert(equal(zix_path_join(NULL, "//host/", "a"), "//host/a"));
  assert(equal(zix_path_join(NULL, "/a", ""), "/a/"));
  assert(equal(zix_path_join(NULL, "/a", "b"), "/a/b"));
  assert(equal(zix_path_join(NULL, "/a", "b/"), "/a/b/"));
  assert(equal(zix_path_join(NULL, "/a", NULL), "/a/"));
  assert(equal(zix_path_join(NULL, "/a/b", NULL), "/a/b/"));
  assert(equal(zix_path_join(NULL, "/a/c", "b"), "/a/c/b"));
  assert(equal(zix_path_join(NULL, "C:", ""), "C:/"));
  assert(equal(zix_path_join(NULL, "C:/a", "/b"), "/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "C:/b"), "C:/a/C:/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "C:b"), "C:/a/C:b"));
  assert(equal(zix_path_join(NULL, "C:/a", "D:b"), "C:/a/D:b"));
  assert(equal(zix_path_join(NULL, "C:/a", "b"), "C:/a/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "b/"), "C:/a/b/"));
  assert(equal(zix_path_join(NULL, "C:/a", "c:/b"), "C:/a/c:/b"));
  assert(equal(zix_path_join(NULL, "C:/a", "c:b"), "C:/a/c:b"));
  assert(equal(zix_path_join(NULL, "C:\\a", "b"), "C:\\a/b"));
  assert(equal(zix_path_join(NULL, "C:\\a", "b\\"), "C:\\a/b\\"));
  assert(equal(zix_path_join(NULL, "C:a", "/b"), "/b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:/b"), "C:a/C:/b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:\\b"), "C:a/C:\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "C:b"), "C:a/C:b"));
  assert(equal(zix_path_join(NULL, "C:a", "b"), "C:a/b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:/b"), "C:a/c:/b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:\\b"), "C:a/c:\\b"));
  assert(equal(zix_path_join(NULL, "C:a", "c:b"), "C:a/c:b"));
  assert(equal(zix_path_join(NULL, "a", ""), "a/"));
  assert(equal(zix_path_join(NULL, "a", "C:"), "a/C:"));
  assert(equal(zix_path_join(NULL, "a", "C:/b"), "a/C:/b"));
  assert(equal(zix_path_join(NULL, "a", "C:\\b"), "a/C:\\b"));
  assert(equal(zix_path_join(NULL, "a", "\\b"), "a/\\b"));
  assert(equal(zix_path_join(NULL, "a", "b"), "a/b"));
  assert(equal(zix_path_join(NULL, "a", "b/"), "a/b/"));
  assert(equal(zix_path_join(NULL, "a", NULL), "a/"));
  assert(equal(zix_path_join(NULL, "a/b", ""), "a/b/"));
  assert(equal(zix_path_join(NULL, "a/b", NULL), "a/b/"));
  assert(equal(zix_path_join(NULL, "a\\", "\\b"), "a\\/\\b"));
  assert(equal(zix_path_join(NULL, "a\\", "b"), "a\\/b"));
#endif
}

static void
test_path_preferred(void)
{
  assert(equal(zix_path_preferred(NULL, ""), ""));
  assert(equal(zix_path_preferred(NULL, "some-name"), "some-name"));
  assert(equal(zix_path_preferred(NULL, "some_name"), "some_name"));

#ifdef _WIN32
  // Backslash is the preferred separator
  assert(equal(zix_path_preferred(NULL, "/"), "\\"));
  assert(equal(zix_path_preferred(NULL, "/."), "\\."));
  assert(equal(zix_path_preferred(NULL, "//a"), "\\\\a"));
  assert(equal(zix_path_preferred(NULL, "//a/"), "\\\\a\\"));
  assert(equal(zix_path_preferred(NULL, "//a//"), "\\\\a\\\\"));
  assert(equal(zix_path_preferred(NULL, "/a//b/c/"), "\\a\\\\b\\c\\"));
  assert(equal(zix_path_preferred(NULL, "/a/b/c/"), "\\a\\b\\c\\"));
  assert(equal(zix_path_preferred(NULL, "\\"), "\\"));
  assert(
    equal(zix_path_preferred(NULL, "\\a\\//b\\/c\\"), "\\a\\\\\\b\\\\c\\"));
  assert(equal(zix_path_preferred(NULL, "\\a\\/b\\/c\\"), "\\a\\\\b\\\\c\\"));
  assert(equal(zix_path_preferred(NULL, "\\a\\b\\c\\"), "\\a\\b\\c\\"));
#else
  // Slash is the only separator
  assert(equal(zix_path_preferred(NULL, "/"), "/"));
  assert(equal(zix_path_preferred(NULL, "/."), "/."));
  assert(equal(zix_path_preferred(NULL, "//a"), "//a"));
  assert(equal(zix_path_preferred(NULL, "//a/"), "//a/"));
  assert(equal(zix_path_preferred(NULL, "//a//"), "//a//"));
  assert(equal(zix_path_preferred(NULL, "/a//b/c/"), "/a//b/c/"));
  assert(equal(zix_path_preferred(NULL, "/a/b/c/"), "/a/b/c/"));
  assert(equal(zix_path_preferred(NULL, "\\"), "\\"));
  assert(equal(zix_path_preferred(NULL, "\\a\\//b\\/c\\"), "\\a\\//b\\/c\\"));
  assert(equal(zix_path_preferred(NULL, "\\a\\/b\\/c\\"), "\\a\\/b\\/c\\"));
  assert(equal(zix_path_preferred(NULL, "\\a\\b\\c\\"), "\\a\\b\\c\\"));
#endif
}

static void
test_path_lexically_normal(void)
{
  // Identities
  assert(equal(zix_path_lexically_normal(NULL, ""), ""));
  assert(equal(zix_path_lexically_normal(NULL, "."), "."));
  assert(equal(zix_path_lexically_normal(NULL, ".."), ".."));
  assert(match(zix_path_lexically_normal(NULL, "../.."), "../.."));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/"), "/a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/c"), "/a/b/c"));
  assert(match(zix_path_lexically_normal(NULL, "a/b"), "a/b"));

  // "Out of bounds" leading dot-dot entries are removed
  assert(match(zix_path_lexically_normal(NULL, "/../"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../.."), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../../"), "/"));

  // Windows drive letters are preserved
  assert(equal(zix_path_lexically_normal(NULL, "C:"), "C:"));
  assert(equal(zix_path_lexically_normal(NULL, "C:a"), "C:a"));
  assert(match(zix_path_lexically_normal(NULL, "C:/"), "C:/"));
  assert(match(zix_path_lexically_normal(NULL, "C:/a"), "C:/a"));
  assert(match(zix_path_lexically_normal(NULL, "C:/a/"), "C:/a/"));
  assert(match(zix_path_lexically_normal(NULL, "C:/a/b"), "C:/a/b"));
  assert(match(zix_path_lexically_normal(NULL, "C:a/"), "C:a/"));

  // Slashes are replaced with a single preferred-separator
  assert(match(zix_path_lexically_normal(NULL, "/"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "//"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "///"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "///a///b/////"), "/a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "/a/b//c"), "/a/b/c"));
  assert(match(zix_path_lexically_normal(NULL, "a///b"), "a/b"));
  assert(match(zix_path_lexically_normal(NULL, "a//b"), "a/b"));
  assert(match(zix_path_lexically_normal(NULL, "a/b"), "a/b"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/"), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b//"), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b///"), "a/b/"));

  // Dot directories are removed
  assert(equal(zix_path_lexically_normal(NULL, "./.."), ".."));
  assert(match(zix_path_lexically_normal(NULL, ".//a//.//./b/.//"), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "./a/././b/./"), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "/."), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/./"), "/a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "a/."), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/./b/."), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/."), "a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/./"), "a/b/"));

  // Real entries before a dot-dot entry are removed
  assert(equal(zix_path_lexically_normal(NULL, "a/.."), "."));
  assert(equal(zix_path_lexically_normal(NULL, "a/../"), "."));
  assert(equal(zix_path_lexically_normal(NULL, "a/b/../.."), "."));
  assert(equal(zix_path_lexically_normal(NULL, "a/b/../../"), "."));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/c/../"), "/a/b/"));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/c/../d"), "/a/b/d"));
  assert(match(zix_path_lexically_normal(NULL, "/a/b/c/../d/"), "/a/b/d/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/.."), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/../"), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/./.."), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/./../"), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/c/../.."), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/b/c/../../"), "a/"));

  // Both dot directories and dot-dot entries
  assert(match(zix_path_lexically_normal(NULL, "a/.///b/../"), "a/"));
  assert(match(zix_path_lexically_normal(NULL, "a/./b/.."), "a/"));

  // Dot-dot entries after a root are removed
  assert(match(zix_path_lexically_normal(NULL, "/.."), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../.."), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../../"), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/../a"), "/a"));
  assert(match(zix_path_lexically_normal(NULL, "/../a/../.."), "/"));
  assert(match(zix_path_lexically_normal(NULL, "/a/../.."), "/"));

  // No trailing separator after a trailing dot-dot entry
  assert(equal(zix_path_lexically_normal(NULL, "../"), ".."));
  assert(match(zix_path_lexically_normal(NULL, "../../"), "../.."));
  assert(match(zix_path_lexically_normal(NULL, "a/../b/../..///"), ".."));
  assert(
    match(zix_path_lexically_normal(NULL, "a/../b/..//..///../"), "../.."));

  // Effectively empty paths are a dot
  assert(equal(zix_path_lexically_normal(NULL, "."), "."));
  assert(equal(zix_path_lexically_normal(NULL, "./"), "."));
  assert(equal(zix_path_lexically_normal(NULL, "./."), "."));
  assert(equal(zix_path_lexically_normal(NULL, "a/.."), "."));

#ifdef _WIN32

  // Paths like "C:/filename" have a drive letter
  assert(equal(zix_path_lexically_normal(NULL, "C:/a\\b"), "C:\\a\\b"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\"), "C:\\"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a"), "C:\\a"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a/"), "C:\\a\\"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a\\"), "C:\\a\\"));
  assert(equal(zix_path_lexically_normal(NULL, "C:a\\"), "C:a\\"));

  // Paths like "//host/dir/" have a network root
  assert(equal(zix_path_lexically_normal(NULL, "//a/"), "\\\\a\\"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/.."), "\\\\a\\"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/b/"), "\\\\a\\b\\"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/b/."), "\\\\a\\b\\"));

#else

  // "C:" is just a strange directory or file name prefix
  assert(equal(zix_path_lexically_normal(NULL, "C:/a\\b"), "C:/a\\b"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\"), "C:\\"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a"), "C:\\a"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a/"), "C:\\a/"));
  assert(equal(zix_path_lexically_normal(NULL, "C:\\a\\"), "C:\\a\\"));
  assert(equal(zix_path_lexically_normal(NULL, "C:a\\"), "C:a\\"));

  // Paths like "//host/dir/" have redundant leading slashes
  assert(equal(zix_path_lexically_normal(NULL, "//a/"), "/a/"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/.."), "/"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/b/"), "/a/b/"));
  assert(equal(zix_path_lexically_normal(NULL, "//a/b/."), "/a/b/"));

#endif
}

static char*
lexically_relative(const char* const path, const char* const base)
{
  return zix_path_lexically_relative(NULL, path, base);
}

static void
test_path_lexically_relative(void)
{
  assert(equal(lexically_relative("", ""), "."));
  assert(equal(lexically_relative("", "."), "."));
  assert(equal(lexically_relative(".", ""), "."));
  assert(equal(lexically_relative(".", "."), "."));
  assert(equal(lexically_relative("//base", "a"), NULL));
  assert(equal(lexically_relative("//host", "//host"), "."));
  assert(equal(lexically_relative("//host", "a"), NULL));
  assert(equal(lexically_relative("//host/", "//host/"), "."));
  assert(equal(lexically_relative("//host/", "a"), NULL));
  assert(equal(lexically_relative("//host/a/b", "//host/a/b"), "."));
  assert(equal(lexically_relative("/a/b", "/a/"), "b"));
  assert(equal(lexically_relative("C:/a/b", "C:/a/"), "b"));
  assert(equal(lexically_relative("a", "/"), NULL));
  assert(equal(lexically_relative("a", "//host"), NULL));
  assert(equal(lexically_relative("a", "//host/"), NULL));
  assert(equal(lexically_relative("a", "a"), "."));
  assert(equal(lexically_relative("a/b", "/a/b"), NULL));
  assert(equal(lexically_relative("a/b", "a/b"), "."));
  assert(equal(lexically_relative("a/b/c", "a"), "b/c"));
  assert(equal(lexically_relative("a/b/c", "a/b/c"), "."));
  assert(equal(lexically_relative("a/b/c/", "a/b/c/"), "."));
  assert(match(lexically_relative("../", "../"), "."));
  assert(match(lexically_relative("../", "./"), "../"));
  assert(match(lexically_relative("../", "a"), "../../"));
  assert(match(lexically_relative("../../a", "../b"), "../../a"));
  assert(match(lexically_relative("../a", "../b"), "../a"));
  assert(match(lexically_relative("/a", "/b/c/"), "../../a"));
  assert(match(lexically_relative("/a/b/c", "/a/b/d/"), "../c"));
  assert(match(lexically_relative("/a/b/c", "/a/b/d/e/"), "../../c"));
  assert(match(lexically_relative("/a/b/c", "/a/d"), "../b/c"));
  assert(match(lexically_relative("/a/d", "/a/b/c"), "../../d"));
  assert(match(lexically_relative("C:/D/", "C:/D/F.txt"), "../"));
  assert(match(lexically_relative("C:/D/F", "C:/D/S/"), "../F"));
  assert(match(lexically_relative("C:/D/F", "C:/G"), "../D/F"));
  assert(match(lexically_relative("C:/D/F.txt", "C:/D/"), "F.txt"));
  assert(match(lexically_relative("C:/E", "C:/D/G"), "../../E"));
  assert(match(lexically_relative("C:/a", "C:/b/c/"), "../../a"));
  assert(match(lexically_relative("C:/a/b/c", "C:/a/b/d/"), "../c"));
  assert(match(lexically_relative("a/b", "c/d"), "../../a/b"));
  assert(match(lexically_relative("a/b/c", "../"), NULL));
  assert(match(lexically_relative("a/b/c", "../../"), NULL));
  assert(match(lexically_relative("a/b/c", "a/b/c/x/y"), "../.."));

#ifdef _WIN32
  assert(equal(lexically_relative("/", "a"), "/"));
  assert(equal(lexically_relative("//host", "//host/"), NULL));
  assert(equal(lexically_relative("//host/", "//host"), "/"));
  assert(equal(lexically_relative("//host/", "/a"), NULL));
  assert(equal(lexically_relative("/a", "//host"), NULL));
  assert(equal(lexically_relative("/a", "//host/"), NULL));
  assert(equal(lexically_relative("C:/D/", "C:F.txt"), NULL));
  assert(equal(lexically_relative("C:/D/S/", "F.txt"), NULL));
  assert(equal(lexically_relative("C:F", "C:/D/"), NULL));
  assert(equal(lexically_relative("C:F", "D:G"), NULL));
  assert(equal(lexically_relative("F", "C:/D/S/"), NULL));
  assert(match(lexically_relative("C:../", "C:../"), "."));
  assert(match(lexically_relative("C:../", "C:./"), "../"));
  assert(match(lexically_relative("C:../", "C:a"), "../../"));
  assert(match(lexically_relative("C:../../a", "C:../b"), "../../a"));
  assert(match(lexically_relative("C:../a", "C:../b"), "../a"));
  assert(match(lexically_relative("C:a/b/c", "C:../"), NULL));
  assert(match(lexically_relative("C:a/b/c", "C:../../"), NULL));
#else
  assert(equal(lexically_relative("/", "a"), NULL));
  assert(equal(lexically_relative("//host", "//host/"), "."));
  assert(equal(lexically_relative("//host/", "//host"), "."));
  assert(equal(lexically_relative("//host/", "/a"), "../host/"));
  assert(equal(lexically_relative("/a", "//host"), "../a"));
  assert(equal(lexically_relative("/a", "//host/"), "../a"));
  assert(equal(lexically_relative("C:/D/", "C:F.txt"), "../C:/D/"));
  assert(equal(lexically_relative("C:/D/S/", "F.txt"), "../C:/D/S/"));
  assert(equal(lexically_relative("C:F", "C:/D/"), "../../C:F"));
  assert(equal(lexically_relative("C:F", "D:G"), "../C:F"));
  assert(equal(lexically_relative("F", "C:/D/S/"), "../../../F"));
  assert(match(lexically_relative("C:../", "C:../"), "."));
  assert(match(lexically_relative("C:../", "C:./"), "../C:../"));
  assert(match(lexically_relative("C:../", "C:a"), "../C:../"));
  assert(match(lexically_relative("C:../../a", "C:../b"), "../../a"));
  assert(match(lexically_relative("C:../a", "C:../b"), "../a"));
  assert(match(lexically_relative("C:a/b/c", "C:../"), "../C:a/b/c"));
  assert(match(lexically_relative("C:a/b/c", "C:../../"), "C:a/b/c"));
#endif
}

static void
test_path_root_name(void)
{
  // Relative paths with no root
  assert(view_equal(zix_path_root_name(""), NULL));
  assert(view_equal(zix_path_root_name("."), NULL));
  assert(view_equal(zix_path_root_name(".."), NULL));
  assert(view_equal(zix_path_root_name("../"), NULL));
  assert(view_equal(zix_path_root_name("./"), NULL));
  assert(view_equal(zix_path_root_name("NONDRIVE:"), NULL));
  assert(view_equal(zix_path_root_name("a"), NULL));
  assert(view_equal(zix_path_root_name("a/"), NULL));
  assert(view_equal(zix_path_root_name("a/."), NULL));
  assert(view_equal(zix_path_root_name("a/.."), NULL));
  assert(view_equal(zix_path_root_name("a/../"), NULL));
  assert(view_equal(zix_path_root_name("a/../b"), NULL));
  assert(view_equal(zix_path_root_name("a/./"), NULL));
  assert(view_equal(zix_path_root_name("a/./b"), NULL));
  assert(view_equal(zix_path_root_name("a/b"), NULL));

  // Absolute paths with a POSIX-style root
  assert(view_equal(zix_path_root_name("/"), NULL));
  assert(view_equal(zix_path_root_name("/."), NULL));
  assert(view_equal(zix_path_root_name("/.."), NULL));
  assert(view_equal(zix_path_root_name("//"), NULL));
  assert(view_equal(zix_path_root_name("///a///"), NULL));
  assert(view_equal(zix_path_root_name("///a///b"), NULL));
  assert(view_equal(zix_path_root_name("/a"), NULL));
  assert(view_equal(zix_path_root_name("/a/"), NULL));
  assert(view_equal(zix_path_root_name("/a//b"), NULL));

#ifdef _WIN32

  // Paths like "C:/filename" have a drive letter
  assert(view_equal(zix_path_root_name("C:"), "C:"));
  assert(view_equal(zix_path_root_name("C:/"), "C:"));
  assert(view_equal(zix_path_root_name("C:/a"), "C:"));
  assert(view_equal(zix_path_root_name("C:/a/"), "C:"));
  assert(view_equal(zix_path_root_name("C:/a/b"), "C:"));
  assert(view_equal(zix_path_root_name("C:a"), "C:"));
  assert(view_equal(zix_path_root_name("C:a/"), "C:"));

  // Paths like "//host/" are network roots
  assert(view_equal(zix_path_root_name("//host"), "//host"));
  assert(view_equal(zix_path_root_name("//host/"), "//host"));
  assert(view_equal(zix_path_root_name("//host/a"), "//host"));

  // Backslash is a directory separator
  assert(view_equal(zix_path_root_name("C:/a\\b"), "C:"));
  assert(view_equal(zix_path_root_name("C:\\"), "C:"));
  assert(view_equal(zix_path_root_name("C:\\a"), "C:"));
  assert(view_equal(zix_path_root_name("C:\\a/"), "C:"));
  assert(view_equal(zix_path_root_name("C:\\a\\"), "C:"));
  assert(view_equal(zix_path_root_name("C:a\\"), "C:"));

#else

  // "C:" is just a strange directory or file name prefix
  assert(view_equal(zix_path_root_name("C:"), NULL));
  assert(view_equal(zix_path_root_name("C:/"), NULL));
  assert(view_equal(zix_path_root_name("C:/a"), NULL));
  assert(view_equal(zix_path_root_name("C:/a/"), NULL));
  assert(view_equal(zix_path_root_name("C:/a/b"), NULL));
  assert(view_equal(zix_path_root_name("C:a"), NULL));
  assert(view_equal(zix_path_root_name("C:a/"), NULL));

  // Paths like "//host/" have redundant leading slashes
  assert(view_equal(zix_path_root_name("//host"), NULL));
  assert(view_equal(zix_path_root_name("//host/"), NULL));
  assert(view_equal(zix_path_root_name("//host/a"), NULL));

  // Backslash is a character in a directory or file name
  assert(view_equal(zix_path_root_name("C:/a\\b"), NULL));
  assert(view_equal(zix_path_root_name("C:\\"), NULL));
  assert(view_equal(zix_path_root_name("C:\\a"), NULL));
  assert(view_equal(zix_path_root_name("C:\\a/"), NULL));
  assert(view_equal(zix_path_root_name("C:\\a\\"), NULL));
  assert(view_equal(zix_path_root_name("C:a\\"), NULL));

#endif
}

static void
test_path_root(void)
{
  // Relative paths with no root
  assert(view_equal(zix_path_root_path(""), NULL));
  assert(view_equal(zix_path_root_path("."), NULL));
  assert(view_equal(zix_path_root_path(".."), NULL));
  assert(view_equal(zix_path_root_path("../"), NULL));
  assert(view_equal(zix_path_root_path("./"), NULL));
  assert(view_equal(zix_path_root_path("NONDRIVE:"), NULL));
  assert(view_equal(zix_path_root_path("a"), NULL));
  assert(view_equal(zix_path_root_path("a/"), NULL));
  assert(view_equal(zix_path_root_path("a/."), NULL));
  assert(view_equal(zix_path_root_path("a/.."), NULL));
  assert(view_equal(zix_path_root_path("a/../"), NULL));
  assert(view_equal(zix_path_root_path("a/../b"), NULL));
  assert(view_equal(zix_path_root_path("a/./"), NULL));
  assert(view_equal(zix_path_root_path("a/./b"), NULL));
  assert(view_equal(zix_path_root_path("a/b"), NULL));

  // Absolute paths with a POSIX-style root
  assert(view_equal(zix_path_root_path("/"), "/"));
  assert(view_equal(zix_path_root_path("/."), "/"));
  assert(view_equal(zix_path_root_path("/.."), "/"));
  assert(view_equal(zix_path_root_path("//"), "/"));
  assert(view_equal(zix_path_root_path("///a///"), "/"));
  assert(view_equal(zix_path_root_path("///a///b"), "/"));
  assert(view_equal(zix_path_root_path("/a"), "/"));
  assert(view_equal(zix_path_root_path("/a/"), "/"));
  assert(view_equal(zix_path_root_path("/a//b"), "/"));

#ifdef _WIN32

  // Paths like "C:/filename" have a drive letter
  assert(view_equal(zix_path_root_path("C:"), "C:"));
  assert(view_equal(zix_path_root_path("C:/"), "C:/"));
  assert(view_equal(zix_path_root_path("C:/a"), "C:/"));
  assert(view_equal(zix_path_root_path("C:/a/"), "C:/"));
  assert(view_equal(zix_path_root_path("C:/a/b"), "C:/"));
  assert(view_equal(zix_path_root_path("C:a"), "C:"));
  assert(view_equal(zix_path_root_path("C:a/"), "C:"));

  // Paths like "//host/" are network roots
  assert(view_equal(zix_path_root_path("//host"), "//host"));
  assert(view_equal(zix_path_root_path("//host/"), "//host/"));
  assert(view_equal(zix_path_root_path("//host/a"), "//host/"));

  // Backslash is a directory separator
  assert(view_equal(zix_path_root_path("C:/a\\b"), "C:/"));
  assert(view_equal(zix_path_root_path("C:\\"), "C:\\"));
  assert(view_equal(zix_path_root_path("C:\\a"), "C:\\"));
  assert(view_equal(zix_path_root_path("C:\\a/"), "C:\\"));
  assert(view_equal(zix_path_root_path("C:\\a\\"), "C:\\"));
  assert(view_equal(zix_path_root_path("C:a\\"), "C:"));

#else

  // "C:" is just a strange directory or file name prefix
  assert(view_equal(zix_path_root_path("C:"), NULL));
  assert(view_equal(zix_path_root_path("C:/"), NULL));
  assert(view_equal(zix_path_root_path("C:/a"), NULL));
  assert(view_equal(zix_path_root_path("C:/a/"), NULL));
  assert(view_equal(zix_path_root_path("C:/a/b"), NULL));
  assert(view_equal(zix_path_root_path("C:a"), NULL));
  assert(view_equal(zix_path_root_path("C:a/"), NULL));

  // Paths like "//host/" have redundant leading slashes
  assert(view_equal(zix_path_root_path("//host"), "/"));
  assert(view_equal(zix_path_root_path("//host/"), "/"));
  assert(view_equal(zix_path_root_path("//host/a"), "/"));

  // Backslash is a character in a directory or file name
  assert(view_equal(zix_path_root_path("C:/a\\b"), NULL));
  assert(view_equal(zix_path_root_path("C:\\"), NULL));
  assert(view_equal(zix_path_root_path("C:\\a"), NULL));
  assert(view_equal(zix_path_root_path("C:\\a/"), NULL));
  assert(view_equal(zix_path_root_path("C:\\a\\"), NULL));
  assert(view_equal(zix_path_root_path("C:a\\"), NULL));

#endif
}

static void
test_path_parent(void)
{
  // Absolute paths
  assert(view_equal(zix_path_parent_path("/"), "/"));
  assert(view_equal(zix_path_parent_path("/."), "/"));
  assert(view_equal(zix_path_parent_path("/.."), "/"));
  assert(view_equal(zix_path_parent_path("//"), "/"));
  assert(view_equal(zix_path_parent_path("/a"), "/"));
  assert(view_equal(zix_path_parent_path("/a/"), "/a"));
  assert(view_equal(zix_path_parent_path("/a//b"), "/a"));

  // Relative paths with no parent
  assert(view_equal(zix_path_parent_path(""), NULL));
  assert(view_equal(zix_path_parent_path("."), NULL));
  assert(view_equal(zix_path_parent_path(".."), NULL));
  assert(view_equal(zix_path_parent_path("NONDRIVE:"), NULL));
  assert(view_equal(zix_path_parent_path("a"), NULL));

  // Relative paths with a parent
  assert(view_equal(zix_path_parent_path("../"), ".."));
  assert(view_equal(zix_path_parent_path("./"), "."));
  assert(view_equal(zix_path_parent_path("a/"), "a"));
  assert(view_equal(zix_path_parent_path("a/b"), "a"));

  // Superfluous leading and trailing separators
  assert(view_equal(zix_path_parent_path("///a///"), "/a"));
  assert(view_equal(zix_path_parent_path("///a///b"), "/a"));

  // Relative paths with dot and dot-dot entries
  assert(view_equal(zix_path_parent_path("a/."), "a"));
  assert(view_equal(zix_path_parent_path("a/.."), "a"));
  assert(view_equal(zix_path_parent_path("a/../"), "a/.."));
  assert(view_equal(zix_path_parent_path("a/../b"), "a/.."));
  assert(view_equal(zix_path_parent_path("a/./"), "a/."));
  assert(view_equal(zix_path_parent_path("a/./b"), "a/."));

#ifdef _WIN32

  // Paths like "C:/filename" have a drive letter
  assert(view_equal(zix_path_parent_path("C:"), "C:"));
  assert(view_equal(zix_path_parent_path("C:/"), "C:/"));
  assert(view_equal(zix_path_parent_path("C:/a"), "C:/"));
  assert(view_equal(zix_path_parent_path("C:/a/"), "C:/a"));
  assert(view_equal(zix_path_parent_path("C:/a/b"), "C:/a"));
  assert(view_equal(zix_path_parent_path("C:/a\\b"), "C:/a"));
  assert(view_equal(zix_path_parent_path("C:\\"), "C:\\"));
  assert(view_equal(zix_path_parent_path("C:\\a"), "C:\\"));
  assert(view_equal(zix_path_parent_path("C:\\a/"), "C:\\a"));
  assert(view_equal(zix_path_parent_path("C:\\a\\"), "C:\\a"));
  assert(view_equal(zix_path_parent_path("C:a"), "C:"));
  assert(view_equal(zix_path_parent_path("C:a/"), "C:a"));
  assert(view_equal(zix_path_parent_path("C:a\\"), "C:a"));

  // Paths like "//host/" are network shares
  assert(view_equal(zix_path_parent_path("//host"), "//host"));
  assert(view_equal(zix_path_parent_path("//host/"), "//host/"));
  assert(view_equal(zix_path_parent_path("//host/a"), "//host/"));

#else

  // "C:" is just a strange directory or file name prefix
  assert(view_equal(zix_path_parent_path("C:"), NULL));
  assert(view_equal(zix_path_parent_path("C:/"), "C:"));
  assert(view_equal(zix_path_parent_path("C:/a"), "C:"));
  assert(view_equal(zix_path_parent_path("C:/a/"), "C:/a"));
  assert(view_equal(zix_path_parent_path("C:/a/b"), "C:/a"));
  assert(view_equal(zix_path_parent_path("C:/a\\b"), "C:"));
  assert(view_equal(zix_path_parent_path("C:\\"), NULL));
  assert(view_equal(zix_path_parent_path("C:\\a"), NULL));
  assert(view_equal(zix_path_parent_path("C:\\a/"), "C:\\a"));
  assert(view_equal(zix_path_parent_path("C:\\a\\"), NULL));
  assert(view_equal(zix_path_parent_path("C:a"), NULL));
  assert(view_equal(zix_path_parent_path("C:a/"), "C:a"));
  assert(view_equal(zix_path_parent_path("C:a\\"), NULL));

  // Paths like "//host/" have redundant leading slashes
  assert(view_equal(zix_path_parent_path("//host"), "/"));
  assert(view_equal(zix_path_parent_path("//host/"), "/host"));
  assert(view_equal(zix_path_parent_path("//host/a"), "/host"));

#endif
}

static void
test_path_filename(void)
{
  // Cases from <https://en.cppreference.com/w/cpp/filesystem/path/filename>
  assert(view_equal(zix_path_filename("."), "."));
  assert(view_equal(zix_path_filename(".."), ".."));
  assert(view_equal(zix_path_filename("/"), NULL));
  assert(view_equal(zix_path_filename("/foo/."), "."));
  assert(view_equal(zix_path_filename("/foo/.."), ".."));
  assert(view_equal(zix_path_filename("/foo/.bar"), ".bar"));
  assert(view_equal(zix_path_filename("/foo/bar.txt"), "bar.txt"));
  assert(view_equal(zix_path_filename("/foo/bar/"), NULL));

  // Identities
  assert(view_equal(zix_path_filename("."), "."));
  assert(view_equal(zix_path_filename(".."), ".."));
  assert(view_equal(zix_path_filename("a"), "a"));

  // Absolute paths without filenames
  assert(view_equal(zix_path_filename("/"), NULL));
  assert(view_equal(zix_path_filename("//"), NULL));
  assert(view_equal(zix_path_filename("///a///"), NULL));
  assert(view_equal(zix_path_filename("/a/"), NULL));

  // Absolute paths with filenames
  assert(view_equal(zix_path_filename("///a///b"), "b"));
  assert(view_equal(zix_path_filename("/a"), "a"));
  assert(view_equal(zix_path_filename("/a//b"), "b"));

  // Relative paths without filenames
  assert(view_equal(zix_path_filename(""), NULL));
  assert(view_equal(zix_path_filename("../"), NULL));
  assert(view_equal(zix_path_filename("./"), NULL));
  assert(view_equal(zix_path_filename("a/"), NULL));

  // Relative paths with filenames
  assert(view_equal(zix_path_filename("a/b"), "b"));

  // Windows absolute network paths conveniently work generically
  assert(view_equal(zix_path_filename("//host/"), NULL));
  assert(view_equal(zix_path_filename("//host/a"), "a"));

  // Paths with dot and dot-dot entries
  assert(view_equal(zix_path_filename("/."), "."));
  assert(view_equal(zix_path_filename("/.."), ".."));
  assert(view_equal(zix_path_filename("a/."), "."));
  assert(view_equal(zix_path_filename("a/.."), ".."));
  assert(view_equal(zix_path_filename("a/../b"), "b"));
  assert(view_equal(zix_path_filename("a/./b"), "b"));

  // Paths with colons (maybe drive letters) that conveniently work generically
  assert(view_equal(zix_path_filename("C:/"), NULL));
  assert(view_equal(zix_path_filename("C:/a"), "a"));
  assert(view_equal(zix_path_filename("C:/a/"), NULL));
  assert(view_equal(zix_path_filename("C:/a/b"), "b"));
  assert(view_equal(zix_path_filename("C:a/"), NULL));
  assert(view_equal(zix_path_filename("NONDRIVE:"), "NONDRIVE:"));

#ifdef _WIN32

  // Relative paths can have a drive letter like "C:file"
  assert(view_equal(zix_path_filename("C:"), NULL));
  assert(view_equal(zix_path_filename("C:a"), "a"));
  assert(view_equal(zix_path_filename("C:a\\"), NULL));

  // Paths like "//host" are network roots
  assert(view_equal(zix_path_filename("//host"), NULL));

  // Backslash is a directory separator
  assert(view_equal(zix_path_filename("C:/a\\b"), "b"));
  assert(view_equal(zix_path_filename("C:\\"), NULL));
  assert(view_equal(zix_path_filename("C:\\a"), "a"));
  assert(view_equal(zix_path_filename("C:\\a/"), NULL));
  assert(view_equal(zix_path_filename("C:\\a\\"), NULL));
  assert(view_equal(zix_path_filename("C:\\a\\b"), "b"));
  assert(view_equal(zix_path_filename("a\\b"), "b"));

#else

  // "C:" is just a strange directory or file name prefix
  assert(view_equal(zix_path_filename("C:"), "C:"));
  assert(view_equal(zix_path_filename("C:a"), "C:a"));
  assert(view_equal(zix_path_filename("C:a\\"), "C:a\\"));

  // Redundant reading slashes are ignored
  assert(view_equal(zix_path_filename("//host"), "host"));

  // Backslash is just a strange character in a directory or file name
  assert(view_equal(zix_path_filename("C:/a\\b"), "a\\b"));
  assert(view_equal(zix_path_filename("C:\\"), "C:\\"));
  assert(view_equal(zix_path_filename("C:\\a"), "C:\\a"));
  assert(view_equal(zix_path_filename("C:\\a/"), NULL));
  assert(view_equal(zix_path_filename("C:\\a\\"), "C:\\a\\"));
  assert(view_equal(zix_path_filename("C:\\a\\b"), "C:\\a\\b"));
  assert(view_equal(zix_path_filename("a\\b"), "a\\b"));

#endif
}

static void
test_path_stem(void)
{
  assert(view_equal(zix_path_stem(""), NULL));
  assert(view_equal(zix_path_stem("."), "."));
  assert(view_equal(zix_path_stem(".."), ".."));
  assert(view_equal(zix_path_stem(".a"), ".a"));
  assert(view_equal(zix_path_stem(".hidden"), ".hidden"));
  assert(view_equal(zix_path_stem(".hidden.txt"), ".hidden"));
  assert(view_equal(zix_path_stem("/"), NULL));
  assert(view_equal(zix_path_stem("//host/name"), "name"));
  assert(view_equal(zix_path_stem("/a."), "a"));
  assert(view_equal(zix_path_stem("/a.txt"), "a"));
  assert(view_equal(zix_path_stem("/a/."), "."));
  assert(view_equal(zix_path_stem("/a/.."), ".."));
  assert(view_equal(zix_path_stem("/a/.hidden"), ".hidden"));
  assert(view_equal(zix_path_stem("/a/b."), "b"));
  assert(view_equal(zix_path_stem("/a/b.tar.gz"), "b.tar"));
  assert(view_equal(zix_path_stem("/a/b.txt"), "b"));
  assert(view_equal(zix_path_stem("/a/b/.hidden"), ".hidden"));
  assert(view_equal(zix_path_stem("C:/name"), "name"));
  assert(view_equal(zix_path_stem("C:dir/name"), "name"));
  assert(view_equal(zix_path_stem("a"), "a"));
  assert(view_equal(zix_path_stem("a."), "a"));
  assert(view_equal(zix_path_stem("a..txt"), "a."));
  assert(view_equal(zix_path_stem("a.txt"), "a"));
  assert(view_equal(zix_path_stem("a/."), "."));
  assert(view_equal(zix_path_stem("a/.."), ".."));
  assert(view_equal(zix_path_stem("a/b."), "b"));
  assert(view_equal(zix_path_stem("a/b.tar.gz"), "b.tar"));
  assert(view_equal(zix_path_stem("a/b.txt"), "b"));

#ifdef _WIN32
  assert(view_equal(zix_path_stem("C:."), "."));
  assert(view_equal(zix_path_stem("C:.a"), ".a"));
  assert(view_equal(zix_path_stem("C:a"), "a"));
#else
  assert(view_equal(zix_path_stem("C:."), "C:"));
  assert(view_equal(zix_path_stem("C:.a"), "C:"));
  assert(view_equal(zix_path_stem("C:a"), "C:a"));
#endif
}

static void
test_path_extension(void)
{
  assert(view_equal(zix_path_extension(""), NULL));
  assert(view_equal(zix_path_extension("."), NULL));
  assert(view_equal(zix_path_extension(".."), NULL));
  assert(view_equal(zix_path_extension(".a"), NULL));
  assert(view_equal(zix_path_extension(".hidden"), NULL));
  assert(view_equal(zix_path_extension(".hidden.txt"), ".txt"));
  assert(view_equal(zix_path_extension("/"), NULL));
  assert(view_equal(zix_path_extension("/a."), "."));
  assert(view_equal(zix_path_extension("/a.txt"), ".txt"));
  assert(view_equal(zix_path_extension("/a/."), NULL));
  assert(view_equal(zix_path_extension("/a/.."), NULL));
  assert(view_equal(zix_path_extension("/a/.hidden"), NULL));
  assert(view_equal(zix_path_extension("/a/b."), "."));
  assert(view_equal(zix_path_extension("/a/b.tar.gz"), ".gz"));
  assert(view_equal(zix_path_extension("/a/b.txt"), ".txt"));
  assert(view_equal(zix_path_extension("/a/b/.hidden"), NULL));
  assert(view_equal(zix_path_extension("C:/.hidden.txt"), ".txt"));
  assert(view_equal(zix_path_extension("C:a.txt"), ".txt"));
  assert(view_equal(zix_path_extension("a"), NULL));
  assert(view_equal(zix_path_extension("a."), "."));
  assert(view_equal(zix_path_extension("a..txt"), ".txt"));
  assert(view_equal(zix_path_extension("a.tar.gz"), ".gz"));
  assert(view_equal(zix_path_extension("a/."), NULL));
  assert(view_equal(zix_path_extension("a/.."), NULL));
  assert(view_equal(zix_path_extension("a/b."), "."));
  assert(view_equal(zix_path_extension("a/b.tar.gz"), ".gz"));
  assert(view_equal(zix_path_extension("a/b.txt"), ".txt"));

#ifdef _WIN32
  assert(view_equal(zix_path_extension("C:."), NULL));
  assert(view_equal(zix_path_extension("C:/.hidden"), NULL));
  assert(view_equal(zix_path_extension("C:/a.txt"), ".txt"));
#else
  assert(view_equal(zix_path_extension("C:."), "."));
  assert(view_equal(zix_path_extension("C:/.hidden"), NULL));
  assert(view_equal(zix_path_extension("C:/a.txt"), ".txt"));
#endif
}

static void
test_path_is_absolute(void)
{
  assert(!zix_path_is_absolute("."));
  assert(!zix_path_is_absolute(".."));
  assert(!zix_path_is_absolute("../"));
  assert(!zix_path_is_absolute("../a"));
  assert(!zix_path_is_absolute("../a/"));
  assert(!zix_path_is_absolute("a"));
  assert(!zix_path_is_absolute("a/b"));
  assert(!zix_path_is_relative("//host/a"));
  assert(zix_path_is_absolute("//host/a"));
  assert(zix_path_is_relative("."));
  assert(zix_path_is_relative(".."));
  assert(zix_path_is_relative("../"));
  assert(zix_path_is_relative("../a"));
  assert(zix_path_is_relative("../a/"));
  assert(zix_path_is_relative("a"));
  assert(zix_path_is_relative("a/b"));

#ifdef _WIN32
  // Paths starting with root names are absolute
  assert(!zix_path_is_absolute("/"));
  assert(!zix_path_is_absolute("/a"));
  assert(!zix_path_is_absolute("/a/b"));
  assert(!zix_path_is_relative("C:/a/b"));
  assert(!zix_path_is_relative("C:\\a\\b"));
  assert(!zix_path_is_relative("D:/a/b"));
  assert(!zix_path_is_relative("D:\\a\\b"));
  assert(zix_path_is_absolute("C:/a/b"));
  assert(zix_path_is_absolute("C:\\a\\b"));
  assert(zix_path_is_absolute("D:/a/b"));
  assert(zix_path_is_absolute("D:\\a\\b"));
  assert(zix_path_is_relative("/"));
  assert(zix_path_is_relative("/a"));
  assert(zix_path_is_relative("/a/b"));
#else
  // Paths starting with slashes are absolute
  assert(!zix_path_is_absolute("C:/a/b"));
  assert(!zix_path_is_absolute("C:\\a\\b"));
  assert(!zix_path_is_absolute("D:/a/b"));
  assert(!zix_path_is_absolute("D:\\a\\b"));
  assert(!zix_path_is_relative("/"));
  assert(!zix_path_is_relative("/a"));
  assert(!zix_path_is_relative("/a/b"));
  assert(zix_path_is_absolute("/"));
  assert(zix_path_is_absolute("/a"));
  assert(zix_path_is_absolute("/a/b"));
  assert(zix_path_is_relative("C:/a/b"));
  assert(zix_path_is_relative("C:\\a\\b"));
  assert(zix_path_is_relative("D:/a/b"));
  assert(zix_path_is_relative("D:\\a\\b"));
#endif
}

static void
test_null_queries(void)
{
  assert(!zix_path_has_root_path(NULL));
  assert(!zix_path_has_root_name(NULL));
  assert(!zix_path_has_root_directory(NULL));
  assert(!zix_path_has_relative_path(NULL));
  assert(!zix_path_has_parent_path(NULL));
  assert(!zix_path_has_filename(NULL));
  assert(!zix_path_has_stem(NULL));
  assert(!zix_path_has_extension(NULL));
  assert(!zix_path_is_absolute(NULL));
  assert(zix_path_is_relative(NULL));
}

typedef char* (*AllocCheckFunc)(ZixAllocator*);

static char*
check_join_alloc(ZixAllocator* const allocator)
{
  return zix_path_join(allocator, "a", "b");
}

static char*
check_join_empty_alloc(ZixAllocator* const allocator)
{
  return zix_path_join(allocator, "", "b");
}

static char*
check_preferred_alloc(ZixAllocator* const allocator)
{
  return zix_path_preferred(allocator, "/a/b");
}

static char*
check_lexically_normal_alloc(ZixAllocator* const allocator)
{
  return zix_path_lexically_normal(allocator, "./a/././b/./");
}

static char*
check_lexically_normal_empty_alloc(ZixAllocator* const allocator)
{
  return zix_path_lexically_normal(allocator, "");
}

static char*
check_lexically_relative_alloc(ZixAllocator* const allocator)
{
  return zix_path_lexically_relative(allocator, "../../a", "../b");
}

static char*
check_lexically_relative_equal_alloc(ZixAllocator* const allocator)
{
  return zix_path_lexically_relative(allocator, "a", "a");
}

static char*
check_lexically_relative_dot_alloc(ZixAllocator* const allocator)
{
  return zix_path_lexically_relative(allocator, "", ".");
}

static void
check_failed_alloc(const AllocCheckFunc check)
{
  ZixFailingAllocator allocator = zix_failing_allocator();

  // Successfully perform check to count the number of allocations
  {
    char* const result = check(&allocator.base);
    assert(result);
    zix_free(&allocator.base, result);
  }

  // Test that each allocation failing is handled gracefully
  const size_t n_new_allocs = zix_failing_allocator_reset(&allocator, 0);
  for (size_t i = 0U; i < n_new_allocs; ++i) {
    zix_failing_allocator_reset(&allocator, i);
    assert(!check(&allocator.base));
  }
}

static void
test_failed_alloc(void)
{
  check_failed_alloc(check_join_alloc);
  check_failed_alloc(check_join_empty_alloc);
  check_failed_alloc(check_preferred_alloc);
  check_failed_alloc(check_lexically_normal_alloc);
  check_failed_alloc(check_lexically_normal_empty_alloc);
  check_failed_alloc(check_lexically_relative_alloc);
  check_failed_alloc(check_lexically_relative_equal_alloc);
  check_failed_alloc(check_lexically_relative_dot_alloc);
}

int
main(void)
{
  test_path_root_name();
  test_path_root();
  test_path_parent();
  test_path_filename();
  test_path_stem();
  test_path_extension();
  test_path_is_absolute();
  test_null_queries();

  test_path_join();
  test_path_preferred();
  test_path_lexically_normal();
  test_path_lexically_relative();

  test_failed_alloc();

  return 0;
}

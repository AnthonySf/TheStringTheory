// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/path.h>

#include "index_range.h"
#include "path_iter.h"
#include "qualifiers.h"

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/string_view.h>

#include <stdbool.h>
#include <string.h>

static ZIX_CONSTEXPR ZixIndexRange two_range = {0U, 2U};
static ZIX_CONSTEXPR ZixIndexRange one_range = {0U, 1U};

#ifdef _WIN32

#  define ZIX_DIR_SEP '\\' // Backslash is preferred on Windows

static inline bool
is_dir_sep(const char c)
{
  return c == '/' || c == '\\';
}

static inline bool
is_any_sep(const char c)
{
  return c == '/' || c == ':' || c == '\\';
}

static inline bool
is_root_name_char(const char c)
{
  return c && !is_dir_sep(c);
}

static ZixIndexRange
zix_path_root_name_range(const char* const path)
{
  ZixIndexRange result = {0U, 0U};

  if (path) {
    if (((path[0] >= 'A' && path[0] <= 'Z') ||
         (path[0] >= 'a' && path[0] <= 'z')) &&
        path[1] == ':') {
      // A root that starts with a letter then ':' has a name like "C:"
      result.end = 2U;

    } else if (is_dir_sep(path[0]) && is_dir_sep(path[1]) &&
               is_root_name_char(path[2])) {
      // A root that starts with two separators has a name like "\\host"
      result.end = 2U;
      while (is_root_name_char(path[result.end])) {
        ++result.end;
      }
    }
  }

  return result;
}

#else

#  define ZIX_DIR_SEP '/' // Slash is the only separator on other platforms

static inline bool
is_dir_sep(const char c)
{
  return c == '/';
}

static inline bool
is_any_sep(const char c)
{
  return c == '/';
}

static inline ZixIndexRange
zix_path_root_name_range(const char* const path)
{
  (void)path;
  return zix_make_range(0U, 0U);
}

#endif

static inline char
normal_path_char(const char c)
{
  return (char)(is_dir_sep(c) ? ZIX_DIR_SEP : c);
}

static ZixStringView
string_view(const char* const path)
{
  const ZixStringView view = {path, strlen(path)};
  return view;
}

typedef struct {
  ZixIndexRange name;
  ZixIndexRange dir;
} ZixRootSlices;

ZIX_PURE_FUNC static ZixRootSlices
zix_path_root_slices(const char* const path)
{
  // A root name not trailed by a separator has no root directory
  const ZixIndexRange name    = zix_path_root_name_range(path);
  const size_t        dir_len = (size_t)(path && is_dir_sep(path[name.end]));
  if (!dir_len) {
    const ZixRootSlices result = {name, {name.end, name.end}};
    return result;
  }

  // Skip redundant root directory separators
  ZixIndexRange dir = {name.end, name.end + dir_len};
  while (is_dir_sep(path[dir.end])) {
    dir.begin = dir.end++;
  }

  const ZixRootSlices result = {name, dir};
  return result;
}

static bool
zix_string_ranges_equal(const char* const   lhs,
                        const ZixIndexRange lhs_range,
                        const char* const   rhs,
                        const ZixIndexRange rhs_range)
{
  const size_t lhs_len = lhs_range.end - lhs_range.begin;
  const size_t rhs_len = rhs_range.end - rhs_range.begin;

  return lhs_len == rhs_len &&
         (lhs_len == 0 ||
          !strncmp(lhs + lhs_range.begin, rhs + rhs_range.begin, lhs_len));
}

ZIX_PURE_FUNC static ZixIndexRange
zix_path_root_path_range(const char* const path)
{
  const ZixRootSlices root    = zix_path_root_slices(path);
  const size_t        dir_len = (size_t)!zix_is_empty_range(root.dir);

  return zix_is_empty_range(root.name)
           ? root.dir
           : zix_make_range(root.name.begin, root.name.end + dir_len);
}

ZIX_PURE_FUNC static ZixIndexRange
zix_path_parent_path_range(const ZixStringView path)
{
  if (!path.length) {
    return zix_make_range(0U, 0U); // Empty paths have no parent
  }

  // Find the first relevant character (skip leading root path if any)
  const ZixIndexRange root = zix_path_root_path_range(path.data);
  const size_t        p    = root.begin;

  // Scan backwards starting at the last character
  size_t l = path.length - 1U;
  if (is_dir_sep(path.data[l])) {
    // Rewind to the last relevant separator (skip trailing redundant ones)
    while (l > p && is_dir_sep(path.data[l - 1U])) {
      --l;
    }
  } else {
    // Rewind to the last relevant character (skip trailing name)
    while (l > p && !is_dir_sep(path.data[l])) {
      --l;
    }
  }

  if (l <= root.end) {
    return root;
  }

  // Drop trailing separators
  while (l > p && is_dir_sep(path.data[l])) {
    --l;
  }

  return zix_make_range(root.begin, root.begin + l + 1U - p);
}

ZIX_PURE_FUNC static ZixIndexRange
zix_path_filename_range(const ZixStringView path)
{
  if (!path.length) {
    return zix_make_range(0, 0);
  }

  // Find the first filename character (skip leading root path if any)
  const size_t begin = zix_path_root_path_range(path.data).end;
  if (begin == path.length || is_dir_sep(path.data[path.length - 1U])) {
    return zix_make_range(0U, 0U);
  }

  // Scan backwards to find the first character after the last separator
  size_t f = path.length - 1U;
  while (f > begin && !is_dir_sep(path.data[f - 1])) {
    --f;
  }

  return zix_make_range(f, path.length);
}

ZIX_PURE_FUNC static ZixIndexRange
zix_path_stem_range(const ZixStringView path)
{
  const ZixIndexRange name = zix_path_filename_range(path);

  ZixIndexRange stem = name;
  if (!zix_is_empty_range(stem) &&
      !zix_string_ranges_equal(path.data, stem, ".", one_range) &&
      !zix_string_ranges_equal(path.data, stem, "..", two_range)) {
    // Scan backwards to the last "." after the last directory separator
    --stem.end;
    while (stem.end > stem.begin && path.data[stem.end] != '.') {
      --stem.end;
    }
  }

  return zix_is_empty_range(stem) ? name : stem;
}

ZIX_PURE_FUNC static ZixIndexRange
zix_path_extension_range(const ZixStringView path)
{
  const ZixIndexRange stem = zix_path_stem_range(path);

  return zix_is_empty_range(stem)
           ? stem
           : zix_make_range(stem.end, stem.end + path.length - stem.end);
}

char*
zix_path_join(ZixAllocator* const allocator,
              const char* const   a,
              const char* const   b)
{
  const ZixStringView b_view = zix_string(b);
  if (!a || !a[0]) {
    return zix_string_view_copy(allocator, b_view);
  }

  const ZixStringView a_view = zix_string(a);
  const ZixRootSlices a_root = zix_path_root_slices(a);
  const ZixRootSlices b_root = zix_path_root_slices(b);

#ifdef _WIN32
  // If the RHS has a different root name, just copy it
  if (b_root.name.end &&
      !zix_string_ranges_equal(a, a_root.name, b, b_root.name)) {
    return zix_string_view_copy(allocator, b_view);
  }
#endif

  // Determine how to join paths
  const bool a_has_root_dir = !zix_is_empty_range(a_root.dir);
  const bool a_has_filename = zix_path_has_filename(a_view.data);
  size_t     prefix_len     = a_view.length;
  bool       add_sep        = false;
  if (!zix_is_empty_range(b_root.dir)) {
    prefix_len = a_root.name.end; // Omit any path from a
  } else if (a_has_filename || (!a_has_root_dir && zix_path_is_absolute(a))) {
    add_sep = true; // Add a preferred separator after a
  }

  const size_t path_len = prefix_len + (size_t)add_sep + b_view.length;
  char* const  path     = (char*)zix_calloc(allocator, path_len + 1U, 1U);

  if (path) {
    memcpy(path, a, prefix_len);

    size_t p = prefix_len;
    if (add_sep) {
      path[p++] = ZIX_DIR_SEP;
    }

    if (b_view.length > b_root.name.end) {
      memcpy(path + p, b + b_root.name.end, b_view.length - b_root.name.end);
      path[p + b_view.length] = '\0';
    }
  }

  return path;
}

char*
zix_path_preferred(ZixAllocator* const allocator, const char* const path)
{
  const ZixStringView path_view = string_view(path);
  char* const result = (char*)zix_calloc(allocator, path_view.length + 1U, 1U);

  if (result) {
    for (size_t i = 0U; i < path_view.length; ++i) {
      result[i] = normal_path_char(path[i]);
    }
  }

  return result;
}

char*
zix_path_lexically_normal(ZixAllocator* const allocator, const char* const path)
{
  ZIX_CONSTEXPR char sep = ZIX_DIR_SEP;

  /* Loosely following the normalization algorithm from
     <https://en.cppreference.com/w/cpp/filesystem/path>, but in such a way
     that only a single buffer (the result) is needed.  This means that
     dot-dot segments are relatively expensive, but it beats a stack in
     everyday common cases. */

  if (!path[0]) {
    return (char*)zix_calloc(allocator, 1U, 1U); // The empty path is normal
  }

  // Allocate a result buffer, with space for one additional character
  const ZixStringView path_view = string_view(path);
  const size_t        path_len  = path_view.length;
  char* const         result = (char*)zix_calloc(allocator, path_len + 2U, 1);
  if (!result) {
    return NULL;
  }

  // Copy root, normalizing separators as we go
  const ZixIndexRange root     = zix_path_root_path_range(path);
  const size_t        root_len = root.end - root.begin;
  size_t              r        = 0U;
  for (size_t i = 0; i < root_len; ++i) {
    result[r++] = normal_path_char(path[i]);
  }

  // Copy path, removing dot entries and collapsing separators as we go
  for (size_t i = root.end; i < path_len; ++i) {
    if (is_dir_sep(path[i])) {
      if (((r == root.end + 1U && result[r - 1] == '.') ||
           (r >= root.end + 2U && result[r - 2] == sep &&
            result[r - 1] == '.'))) {
        // Remove dot entry and any immediately following separators
        result[--r] = '\0';

      } else {
        // Replace separators with a single preferred separator
        result[r++] = sep;
      }

      // Collapse redundant separators
      while (is_dir_sep(path[i + 1])) {
        ++i;
      }

    } else {
      result[r++] = path[i];
    }
  }

  // Collapse any dot-dot entries following a directory name
  size_t last = r;
  size_t next = 0;
  for (size_t i = root_len; i < r;) {
    if (last < r && i > 2U && result[i - 2U] == sep && result[i - 1U] == '.' &&
        result[i] == '.' && (!result[i + 1U] || is_dir_sep(result[i + 1U]))) {
      if (result[i + 1] == sep) {
        ++i;
      }

      const size_t suffix_len = r - i - 1U;
      memmove(result + last, result + i + 1, suffix_len);
      r         = r - ((r - last) - suffix_len);
      result[r] = '\0';
      i         = 0;
      last      = r;
      next      = 0;
    } else {
      if (i >= 1 && result[i - 1] == sep) {
        next = i;
      }

      if (result[i] != sep && result[i] != '.') {
        last = next;
      }
      ++i;
    }
  }

  // Remove any dot-dot entries following the root directory
  if (root_len && is_dir_sep(result[root_len - 1U])) {
    size_t start = root_len;
    while (start < r && result[start] == '.' && result[start + 1] == '.' &&
           (result[start + 2] == sep || result[start + 2] == '\0')) {
      start += (result[start + 2] == sep) ? 3U : 2U;
    }

    if (start > root_len) {
      if (start < r) {
        memmove(result + root_len, result + start, r - start);
        r = root_len + r - start;
      } else {
        r = root_len;
      }

      result[r] = '\0';
      return result;
    }
  }

  // Remove trailing dot entry
  if (r >= 2U && is_any_sep(result[r - 2]) && result[r - 1] == '.') {
    result[r - 1] = '\0';
  }

  // Remove trailing dot-dot entry
  if (r >= 3U && result[r - 3] == '.' && result[r - 2] == '.' &&
      is_any_sep(result[r - 1])) {
    result[r - 1] = '\0';
  }

  // If the path is empty, add a dot
  if (!result[0]) {
    result[0] = '.';
    result[1] = '\0';
  }

  return result;
}

static ZixPathIter
make_iter(const ZixPathIterState state, const ZixIndexRange range)
{
  const ZixPathIter result = {range, state};
  return result;
}

ZixPathIter
zix_path_begin(const char* const path)
{
  const ZixPathIter iter = {zix_path_root_name_range(path), ZIX_PATH_ROOT_NAME};

  return (iter.range.end > iter.range.begin) ? iter : zix_path_next(path, iter);
}

ZixPathIter
zix_path_next(const char* const path, ZixPathIter iter)
{
  if (iter.state == ZIX_PATH_ROOT_NAME) {
    if (is_dir_sep(path[iter.range.end])) {
      return make_iter(ZIX_PATH_ROOT_DIRECTORY,
                       zix_make_range(iter.range.end, iter.range.end + 1U));
    }
  }

  if (iter.state <= ZIX_PATH_ROOT_DIRECTORY) {
    iter.range.begin = iter.range.end;
    iter.state       = ZIX_PATH_FILE_NAME;
  }

  if (iter.state == ZIX_PATH_FILE_NAME) {
    // If the last range end was the end of the path, we're done
    iter.range.begin = iter.range.end;
    if (!path[iter.range.begin]) {
      return make_iter(ZIX_PATH_END, iter.range);
    }

    // Skip any leading separators
    while (is_dir_sep(path[iter.range.begin])) {
      iter.range.begin = ++iter.range.end;
    }

    // Advance end to the next separator or path end
    while (path[iter.range.end] && !is_dir_sep(path[iter.range.end])) {
      ++iter.range.end;
    }
  }

  return iter;
}

static size_t
zix_path_append(char* const       buf,
                const size_t      offset,
                const char* const string,
                const size_t      length)
{
  size_t o = offset;
  if (offset) {
    buf[o++] = ZIX_DIR_SEP;
  }

  memcpy(buf + o, string, length);
  return o + length;
}

char*
zix_path_lexically_relative(ZixAllocator* const allocator,
                            const char* const   path,
                            const char* const   base)
{
  const ZixStringView dot = {".", 1U};

  // Mismatched roots can't be expressed in relative form
  const ZixRootSlices path_root         = zix_path_root_slices(path);
  const ZixRootSlices base_root         = zix_path_root_slices(base);
  const bool          path_has_root_dir = !zix_is_empty_range(path_root.dir);
  const bool          base_has_root_dir = !zix_is_empty_range(base_root.dir);
  if (!zix_string_ranges_equal(path, path_root.name, base, base_root.name) ||
      (zix_path_is_absolute(path) != zix_path_is_absolute(base)) ||
      (!path_has_root_dir && base_has_root_dir)) {
    return NULL;
  }

  // Find the first mismatching element in the paths (or the end)
  ZixPathIter a = zix_path_begin(path);
  ZixPathIter b = zix_path_begin(base);
  while (a.state != ZIX_PATH_END && a.state == b.state &&
         zix_string_ranges_equal(path, a.range, base, b.range)) {
    a = zix_path_next(path, a);
    b = zix_path_next(base, b);
  }

  // Matching paths reduce to "."
  if ((a.state == ZIX_PATH_END && b.state == ZIX_PATH_END) ||
      (zix_is_empty_range(a.range) && b.state == ZIX_PATH_END)) {
    return zix_string_view_copy(allocator, dot);
  }

  // Count the trailing non-matching entries in base
  size_t n_base_up   = 0U;
  size_t n_non_empty = 0U;
  for (; b.state < ZIX_PATH_END; b = zix_path_next(base, b)) {
    if (!zix_is_empty_range(b.range)) {
      if (zix_string_ranges_equal(base, b.range, "..", two_range)) {
        ++n_base_up;
      } else if (!zix_string_ranges_equal(base, b.range, ".", one_range)) {
        ++n_non_empty;
      }
    }
  }

  // Base can't have too many up-references
  if (n_base_up > n_non_empty) {
    return NULL;
  }

  const size_t n_up =
    (a.state == ZIX_PATH_ROOT_DIRECTORY) ? 0U : (n_non_empty - n_base_up);

  // A result with no up-references or names reduces to "."
  if (n_up == 0 && a.state == ZIX_PATH_END) {
    return zix_string_view_copy(allocator, dot);
  }

  // Allocate buffer for relative path result
  const size_t path_len = strlen(path);
  const size_t rel_len  = (n_up * 3U) + path_len - a.range.begin;
  char* const  rel      = (char*)zix_calloc(allocator, rel_len + 1U, 1U);
  if (!rel) {
    return NULL;
  }

  // Write leading up-references
  size_t offset = 0U;
  for (size_t i = 0U; i < n_up; ++i) {
    offset = zix_path_append(rel, offset, "..", 2U);
  }

  const char path_last = path[path_len - 1U];
  if (a.range.begin < path_len) {
    // Copy suffix from path (from `a` to the end)
    const size_t suffix_len = path_len - a.range.begin;
    offset = zix_path_append(rel, offset, path + a.range.begin, suffix_len);
  } else if (n_up && path_len > 1 && is_dir_sep(path_last)) {
    // Copy trailing directory separator from path
    rel[offset++] = path_last;
  }

  rel[offset++] = '\0';
  return rel;
}

// Decomposition

static ZixStringView
range_string_view(const char* const path, const ZixIndexRange range)
{
  return zix_substring(path + range.begin, range.end - range.begin);
}

#ifdef _WIN32

ZIX_NONBLOCKING ZixStringView
zix_path_root_name(const char* const path)
{
  return range_string_view(path, zix_path_root_name_range(path));
}

#else

ZIX_NONBLOCKING ZixStringView
zix_path_root_name(const char* const path)
{
  (void)path;
  return zix_empty_string();
}

#endif

ZIX_NONBLOCKING ZixStringView
zix_path_root_directory(const char* const path)
{
  return range_string_view(path, zix_path_root_slices(path).dir);
}

ZIX_NONBLOCKING ZixStringView
zix_path_root_path(const char* const path)
{
  return range_string_view(path, zix_path_root_path_range(path));
}

ZIX_NONBLOCKING ZixStringView
zix_path_relative_path(const char* const path)
{
  const ZixStringView view = string_view(path);
  const ZixIndexRange root = zix_path_root_path_range(path);

  return range_string_view(path, zix_make_range(root.end, view.length));
}

ZIX_NONBLOCKING ZixStringView
zix_path_parent_path(const char* const path)
{
  return range_string_view(path, zix_path_parent_path_range(string_view(path)));
}

ZIX_NONBLOCKING ZixStringView
zix_path_filename(const char* const path)
{
  return range_string_view(path, zix_path_filename_range(string_view(path)));
}

ZIX_NONBLOCKING ZixStringView
zix_path_stem(const char* const path)
{
  return range_string_view(path, zix_path_stem_range(string_view(path)));
}

ZIX_NONBLOCKING ZixStringView
zix_path_extension(const char* const path)
{
  return range_string_view(path, zix_path_extension_range(string_view(path)));
}

// Queries

ZIX_NONBLOCKING bool
zix_path_has_root_path(const char* const path)
{
  return !zix_is_empty_range(zix_path_root_path_range(path));
}

ZIX_NONBLOCKING bool
zix_path_has_root_name(const char* const path)
{
  return !zix_is_empty_range(zix_path_root_name_range(path));
}

ZIX_NONBLOCKING bool
zix_path_has_root_directory(const char* const path)
{
  return !zix_is_empty_range(zix_path_root_slices(path).dir);
}

ZIX_NONBLOCKING bool
zix_path_has_relative_path(const char* const path)
{
  return path && path[zix_path_root_path_range(path).end];
}

ZIX_NONBLOCKING bool
zix_path_has_parent_path(const char* const path)
{
  return !zix_is_empty_range(zix_path_parent_path_range(zix_string(path)));
}

ZIX_NONBLOCKING bool
zix_path_has_filename(const char* const path)
{
  return !zix_is_empty_range(zix_path_filename_range(zix_string(path)));
}

ZIX_NONBLOCKING bool
zix_path_has_stem(const char* const path)
{
  return !zix_is_empty_range(zix_path_stem_range(zix_string(path)));
}

ZIX_NONBLOCKING bool
zix_path_has_extension(const char* const path)
{
  return !zix_is_empty_range(zix_path_extension_range(zix_string(path)));
}

ZIX_NONBLOCKING bool
zix_path_is_absolute(const char* const path)
{
#ifdef _WIN32
  const ZixRootSlices root = zix_path_root_slices(path);
  return (!zix_is_empty_range(root.name) &&
          (!zix_is_empty_range(root.dir) ||
           (is_dir_sep(path[0]) && is_dir_sep(path[1]))));

#else
  return path && is_dir_sep(path[0]);
#endif
}

ZIX_NONBLOCKING bool
zix_path_is_relative(const char* const path)
{
  return !zix_path_is_absolute(path);
}

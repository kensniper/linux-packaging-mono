/**
 * \file
 * Copyright 2018 Microsoft
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */
#ifndef __MONO_METHOD_BUILDER_ILGEN_INTERNALS_H__
#define __MONO_METHOD_BUILDER_ILGEN_INTERNALS_H__

#include "config.h"
#include <mono/metadata/class.h>
#include <mono/metadata/object-internals.h>
#include <mono/metadata/class-internals.h>
#include <mono/metadata/opcodes.h>
#include <mono/metadata/reflection.h>
#include <mono/metadata/method-builder.h>

/* ilgen version */
struct _MonoMethodBuilder {
	MonoMethod *method;
	char *name;
	gboolean no_dup_name;
	GList *locals_list;
	int locals;
	gboolean dynamic;
	gboolean skip_visibility, init_locals;
	guint32 code_size, pos;
	unsigned char *code;
	int num_clauses;
	MonoExceptionClause *clauses;
	const char **param_names;
};

#endif

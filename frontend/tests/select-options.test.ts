import { test } from "node:test";
import assert from "node:assert/strict";
import { createElement as h, Fragment } from "react";
import { collectOptions } from "../src/components/ui/select-options.ts";

// Mirrors the action-type select in PersonnelActionsPage: {x.name}{suffix} gives an array child.
const types = [
  { id: 7, name: "تعيين", workflowDefinitionId: 3 },
  { id: 9, name: "نقل", workflowDefinitionId: null },
];
const noPath = "بلا مسار";

test("action-type options show the Arabic name while the value stays the id", () => {
  const opts = collectOptions([
    h("option", { key: "", value: "" }, "اختر النوع"),
    ...types.map((x) => h("option", { key: x.id, value: x.id }, x.name, x.workflowDefinitionId ? "" : ` (${noPath})`)),
  ]);
  assert.deepEqual(opts.map((o) => [o.value, o.label]), [
    ["", "اختر النوع"],
    ["7", "تعيين"],
    ["9", `نقل (${noPath})`],
  ]);
});

test("mixed text children are joined, not replaced by the value", () => {
  const [o] = collectOptions(h("option", { value: 5 }, "T-90001", " · ", "موظف تجريبي"));
  assert.equal(o.label, "T-90001 · موظف تجريبي");
  assert.equal(o.value, "5");
});

test("options inside fragments and optgroups are collected; empty option falls back to value", () => {
  const opts = collectOptions(h(Fragment, null,
    h("option", { value: "" }),
    h("optgroup", { label: "g" }, h("option", { value: 1 }, h(Fragment, null, "أ", 1)))));
  assert.deepEqual(opts.map((o) => [o.value, o.label]), [["", ""], ["1", "أ1"]]);
});

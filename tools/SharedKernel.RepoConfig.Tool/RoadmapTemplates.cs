namespace SharedKernel.RepoConfig.Tool;

internal static class RoadmapTemplates
{
    public const string RoadmapReadme = """
        # Roadmap

        This folder is the source of truth for roadmap intent and prioritization inputs.
        GitHub Issues and Projects are execution views derived from this data.

        Run the repo config tool to verify the structure:

        ```bash
        sharedkernel-repo verify
        ```
        """;

    public const string ConfigJson = """
        {
          "$schema": "./schema/roadmap-config.schema.json",
          "schemaVersion": "1.0",
          "sourceOfTruth": "repository",
          "itemIdPrefix": "RM",
          "allowed": {
            "types": [
              "epic",
              "issue",
              "feature",
              "enabler",
              "blocker",
              "risk",
              "documentation"
            ],
            "statuses": [
              "proposed",
              "ready",
              "in_progress",
              "done",
              "dropped"
            ]
          },
          "project": {
            "ordering": "order",
            "blockedBy": "blockedBy",
            "closedStatuses": [
              "done",
              "dropped"
            ]
          },
          "scoring": {
            "model": "RICE",
            "formula": "reach * impact * confidence / effort"
          },
          "integrations": {
            "github": {
              "enabled": false,
              "sourceOfTruth": "projection",
              "intake": {
                "theme": "repo-operations",
                "openStatus": "proposed",
                "closedStatus": "done"
              }
            }
          }
        }
        """;

    public const string ConfigSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Roadmap configuration",
          "type": "object",
          "required": [
            "schemaVersion",
            "sourceOfTruth",
            "itemIdPrefix",
            "allowed",
            "project",
            "scoring",
            "integrations"
          ],
          "properties": {
            "schemaVersion": {
              "type": "string"
            },
            "sourceOfTruth": {
              "const": "repository"
            },
            "itemIdPrefix": {
              "type": "string",
              "minLength": 1,
              "pattern": "^[A-Za-z0-9_-]+$"
            },
            "allowed": {
              "type": "object",
              "required": [
                "types",
                "statuses"
              ],
              "properties": {
                "types": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  },
                  "minItems": 1,
                  "uniqueItems": true
                },
                "statuses": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  },
                  "minItems": 1,
                  "uniqueItems": true
                }
              }
            },
            "scoring": {
              "type": "object",
              "required": [
                "model",
                "formula"
              ],
              "properties": {
                "model": {
                  "const": "RICE"
                },
                "formula": {
                  "const": "reach * impact * confidence / effort"
                },
                "ranges": {
                  "type": "object",
                  "properties": {
                    "reach": {
                      "type": "string"
                    },
                    "impact": {
                      "type": "string"
                    },
                    "confidence": {
                      "type": "string"
                    },
                    "effort": {
                      "type": "string"
                    }
                  }
                }
              }
            },
            "project": {
              "type": "object",
              "required": [
                "ordering",
                "blockedBy",
                "closedStatuses"
              ],
              "properties": {
                "ordering": {
                  "const": "order"
                },
                "blockedBy": {
                  "const": "blockedBy"
                },
                "closedStatuses": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  },
                  "minItems": 1,
                  "uniqueItems": true
                }
              },
              "additionalProperties": false
            },
            "integrations": {
              "type": "object",
              "properties": {
                "github": {
                  "type": "object",
                  "properties": {
                    "enabled": {
                      "type": "boolean"
                    },
                    "repository": {
                      "type": "string"
                    },
                    "sourceOfTruth": {
                      "const": "projection"
                    },
                    "intake": {
                      "type": "object",
                      "required": [
                        "theme",
                        "openStatus",
                        "closedStatus"
                      ],
                      "properties": {
                        "theme": {
                          "type": "string",
                          "minLength": 1
                        },
                        "openStatus": {
                          "type": "string",
                          "minLength": 1
                        },
                        "closedStatus": {
                          "type": "string",
                          "minLength": 1
                        }
                      },
                      "additionalProperties": false
                    },
                    "projectV2": {
                      "type": "object",
                      "required": [
                        "id",
                        "owner",
                        "number"
                      ],
                      "properties": {
                        "id": {
                          "type": "string",
                          "minLength": 1
                        },
                        "owner": {
                          "type": "string",
                          "minLength": 1
                        },
                        "number": {
                          "type": "integer",
                          "minimum": 1
                        }
                      },
                      "additionalProperties": false
                    }
                  },
                  "additionalProperties": false
                }
              }
            }
          }
        }
        """;

    public const string OrderJson = """
        {
          "ordering": "lower order values are higher priority",
          "items": [
            "RM-001"
          ]
        }
        """;

    public const string ItemSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Roadmap item",
          "type": "object",
          "required": [
            "id",
            "title",
            "type",
             "status",
             "theme",
             "outcome",
            "blockedBy",
            "blocks",
            "dependencies",
            "tags",
            "labels"
          ],
          "properties": {
            "id": {
              "type": "string",
              "minLength": 1
            },
            "title": {
              "type": "string",
              "minLength": 1
            },
            "type": {
              "type": "string"
            },
            "status": {
              "type": "string"
            },
            "triage": {
              "const": "untriaged"
            },
            "order": {
              "type": "integer",
              "minimum": 1
            },
            "parent": {
              "type": "string",
              "minLength": 1,
              "pattern": "\\S"
            },
            "theme": {
              "type": "string",
              "minLength": 1
            },
            "outcome": {
              "type": "string",
              "minLength": 1
            },
            "size": {
              "type": "string",
              "enum": [
                "XS",
                "S",
                "M",
                "L",
                "XL"
              ]
            },
            "scoring": {
              "type": "object",
              "required": [
                "reach",
                "impact",
                "confidence",
                "effort"
              ],
              "properties": {
                "reach": {
                  "type": "number",
                  "minimum": 0
                },
                "impact": {
                  "type": "number",
                  "minimum": 1,
                  "maximum": 5
                },
                "confidence": {
                  "type": "number",
                  "minimum": 0.1,
                  "maximum": 1
                },
                "effort": {
                  "type": "number",
                  "exclusiveMinimum": 0
                }
              }
            },
            "dependencies": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "uniqueItems": true
            },
            "blockedBy": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "uniqueItems": true
            },
            "blocks": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "uniqueItems": true
            },
            "tags": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "uniqueItems": true
            },
            "labels": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "uniqueItems": true
            },
            "sources": {
              "type": "array"
            },
            "integrations": {
              "type": "object",
              "properties": {
                "github": {
                  "type": "object",
                  "properties": {
                    "issue": {
                      "oneOf": [
                        {
                          "type": "integer",
                          "minimum": 1
                        },
                        {
                          "const": "create"
                        }
                      ]
                    }
                  },
                  "additionalProperties": false
                }
              }
            }
          },
          "allOf": [
            {
              "if": {
                "required": [
                  "triage"
                ],
                "properties": {
                  "triage": {
                    "const": "untriaged"
                  }
                }
              },
              "then": {
                "not": {
                  "anyOf": [
                    {
                      "required": [
                        "order"
                      ]
                    },
                    {
                      "required": [
                        "scoring"
                      ]
                    }
                  ]
                }
              },
              "else": {
                "required": [
                  "order",
                  "scoring"
                ]
              }
            }
          ]
        }
        """;

    public const string DefaultThemeJson = """
        {
          "id": "repo-operations",
          "title": "Repository operations",
          "description": "Repository structure, contributor workflow, local tooling, and planning automation."
        }
        """;

    public const string DefaultItemJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-001",
          "title": "Establish GitOps roadmap and repo configuration tooling",
          "type": "epic",
          "status": "ready",
          "order": 10,
          "theme": "repo-operations",
          "outcome": "Roadmap intent and prioritization are managed through reviewed repository changes and projected into GitHub for execution.",
          "scoring": {
            "reach": 20,
            "impact": 4,
            "confidence": 0.8,
            "effort": 5
          },
          "blockedBy": [],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "gitops",
            "roadmap",
            "repo-config"
          ],
          "labels": [
            "area: tooling",
            "configuration",
            "documentation"
          ]
        }
        """;
}

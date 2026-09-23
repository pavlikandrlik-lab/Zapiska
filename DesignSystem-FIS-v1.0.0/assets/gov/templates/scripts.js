(() => {
  // node_modules/tslib/tslib.es6.mjs
  function __awaiter(thisArg, _arguments, P, generator) {
    function adopt(value) {
      return value instanceof P ? value : new P(function(resolve) {
        resolve(value);
      });
    }
    return new (P || (P = Promise))(function(resolve, reject) {
      function fulfilled(value) {
        try {
          step(generator.next(value));
        } catch (e) {
          reject(e);
        }
      }
      function rejected(value) {
        try {
          step(generator["throw"](value));
        } catch (e) {
          reject(e);
        }
      }
      function step(result) {
        result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected);
      }
      step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
  }
  function __generator(thisArg, body) {
    var _ = { label: 0, sent: function() {
      if (t[0] & 1) throw t[1];
      return t[1];
    }, trys: [], ops: [] }, f, y, t, g = Object.create((typeof Iterator === "function" ? Iterator : Object).prototype);
    return g.next = verb(0), g["throw"] = verb(1), g["return"] = verb(2), typeof Symbol === "function" && (g[Symbol.iterator] = function() {
      return this;
    }), g;
    function verb(n) {
      return function(v) {
        return step([n, v]);
      };
    }
    function step(op) {
      if (f) throw new TypeError("Generator is already executing.");
      while (g && (g = 0, op[0] && (_ = 0)), _) try {
        if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
        if (y = 0, t) op = [op[0] & 2, t.value];
        switch (op[0]) {
          case 0:
          case 1:
            t = op;
            break;
          case 4:
            _.label++;
            return { value: op[1], done: false };
          case 5:
            _.label++;
            y = op[1];
            op = [0];
            continue;
          case 7:
            op = _.ops.pop();
            _.trys.pop();
            continue;
          default:
            if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) {
              _ = 0;
              continue;
            }
            if (op[0] === 3 && (!t || op[1] > t[0] && op[1] < t[3])) {
              _.label = op[1];
              break;
            }
            if (op[0] === 6 && _.label < t[1]) {
              _.label = t[1];
              t = op;
              break;
            }
            if (t && _.label < t[2]) {
              _.label = t[2];
              _.ops.push(op);
              break;
            }
            if (t[2]) _.ops.pop();
            _.trys.pop();
            continue;
        }
        op = body.call(thisArg, _);
      } catch (e) {
        op = [6, e];
        y = 0;
      } finally {
        f = t = 0;
      }
      if (op[0] & 5) throw op[1];
      return { value: op[0] ? op[1] : void 0, done: true };
    }
  }

  // node_modules/@gov-design-system-ce/utils/dist/bool.utils.js
  function toBool(value) {
    if (typeof value === "string") {
      return ["true", "True", "TRUE", "1"].indexOf(value) !== -1;
    } else if (typeof value === "number") {
      return value > 0;
    } else if (typeof value === "boolean") {
      return value;
    } else {
      return !!value;
    }
  }

  // node_modules/@gov-design-system-ce/utils/dist/dom.utils.js
  var firstFocusableElement = (el) => {
    const focusable = el.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    return focusable.length ? focusable[0] : null;
  };
  var smoothScrollTo = (y, duration = 500) => {
    const start = window.scrollY || document.documentElement.scrollTop;
    const distance = y - start;
    let startTime = null;
    function animation(currentTime) {
      if (!startTime)
        startTime = currentTime;
      const timeElapsed = currentTime - startTime;
      const progress = Math.min(timeElapsed / duration, 1);
      const ease = progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.pow(-2 * progress + 2, 3) / 2;
      window.scrollTo(0, start + distance * ease);
      if (progress < 1) {
        requestAnimationFrame(animation);
      }
    }
    requestAnimationFrame(animation);
  };

  // node_modules/ssr-window/ssr-window.esm.js
  function isObject(obj) {
    return obj !== null && typeof obj === "object" && "constructor" in obj && obj.constructor === Object;
  }
  function extend(target = {}, src = {}) {
    const noExtend = ["__proto__", "constructor", "prototype"];
    Object.keys(src).filter((key) => noExtend.indexOf(key) < 0).forEach((key) => {
      if (typeof target[key] === "undefined")
        target[key] = src[key];
      else if (isObject(src[key]) && isObject(target[key]) && Object.keys(src[key]).length > 0) {
        extend(target[key], src[key]);
      }
    });
  }
  var ssrDocument = {
    body: {},
    addEventListener() {
    },
    removeEventListener() {
    },
    activeElement: {
      blur() {
      },
      nodeName: ""
    },
    querySelector() {
      return null;
    },
    querySelectorAll() {
      return [];
    },
    getElementById() {
      return null;
    },
    createEvent() {
      return {
        initEvent() {
        }
      };
    },
    createElement() {
      return {
        children: [],
        childNodes: [],
        style: {},
        setAttribute() {
        },
        getElementsByTagName() {
          return [];
        }
      };
    },
    createElementNS() {
      return {};
    },
    importNode() {
      return null;
    },
    location: {
      hash: "",
      host: "",
      hostname: "",
      href: "",
      origin: "",
      pathname: "",
      protocol: "",
      search: ""
    }
  };
  function getDocument() {
    const doc = typeof document !== "undefined" ? document : {};
    extend(doc, ssrDocument);
    return doc;
  }
  var ssrWindow = {
    document: ssrDocument,
    navigator: {
      userAgent: ""
    },
    location: {
      hash: "",
      host: "",
      hostname: "",
      href: "",
      origin: "",
      pathname: "",
      protocol: "",
      search: ""
    },
    history: {
      replaceState() {
      },
      pushState() {
      },
      go() {
      },
      back() {
      }
    },
    CustomEvent: function CustomEvent() {
      return this;
    },
    addEventListener() {
    },
    removeEventListener() {
    },
    getComputedStyle() {
      return {
        getPropertyValue() {
          return "";
        }
      };
    },
    Image() {
    },
    Date() {
    },
    screen: {},
    setTimeout() {
    },
    clearTimeout() {
    },
    matchMedia() {
      return {};
    },
    requestAnimationFrame(callback) {
      if (typeof setTimeout === "undefined") {
        callback();
        return null;
      }
      return setTimeout(callback, 0);
    },
    cancelAnimationFrame(id) {
      if (typeof setTimeout === "undefined") {
        return;
      }
      clearTimeout(id);
    }
  };
  function getWindow() {
    const win = typeof window !== "undefined" ? window : {};
    extend(win, ssrWindow);
    return win;
  }

  // node_modules/@gov-design-system-ce/utils/dist/string.utils.js
  function chr4() {
    return Math.random().toString(16).slice(-4);
  }
  function createID(prefix) {
    return `${prefix}-${chr4()}${chr4()}-${chr4()}-${chr4()}-${chr4()}-${chr4()}${chr4()}${chr4()}`;
  }

  // node_modules/@gov-design-system-ce/utils/dist/time.utils.js
  function delay(miliseconds) {
    return __awaiter(this, void 0, void 0, function* () {
      return new Promise((resolve) => setTimeout(resolve, miliseconds));
    });
  }
  function blockRapidClicks(callback, limit = 200) {
    let lastClickTime = 0;
    return () => {
      const now = Date.now();
      if (now - lastClickTime > limit) {
        callback();
      }
      lastClickTime = now;
    };
  }
  var debounce = blockRapidClicks;

  // node_modules/@gov-design-system-ce/templates/dist/scripts/main-navigation.js
  var TABLET_SIZE = 768;
  var windowWidth = window.innerWidth;
  var MainNavigation = (
    /** @class */
    (function() {
      function MainNavigation2(rootElement) {
        this.rootElement = rootElement;
        this.verifyAndFixAccessibility(this.rootUlElement);
        this.calculateSizeOfElements();
        this.registerListeners();
        this.accessibilityMobileNavigation().catch();
        if (getWindow().innerWidth >= TABLET_SIZE) {
          this.moveDeferredItems().catch();
          this.controlHeaderNavigation();
        } else {
          this.controlHeaderNavigation(false);
        }
      }
      MainNavigation2.prototype.registerListeners = function() {
        var _this = this;
        this.registerClickTriggers();
        getWindow().addEventListener("resize", debounce(function() {
          var isRealResize = windowWidth !== window.innerWidth;
          if (isRealResize) {
            windowWidth = window.innerWidth;
          } else {
            return;
          }
          _this.resetDeferredMenu();
          if (getWindow().innerWidth >= TABLET_SIZE) {
            _this.controlHeaderNavigation();
            _this.moveDeferredItems().catch();
          } else if (!_this.isMobileMenuOpen()) {
            _this.controlHeaderNavigation(false);
          }
        }, 50));
      };
      MainNavigation2.prototype.isMobileMenuOpen = function() {
        var trigger = getDocument().querySelector(".js-gov-header__navigation-trigger");
        if (!trigger)
          return false;
        return toBool(this.resolveTriggerType(trigger).getAttribute("aria-expanded"));
      };
      MainNavigation2.prototype.moveDeferredItems = function() {
        return __awaiter(this, void 0, void 0, function() {
          var gap, size;
          var _this = this;
          return __generator(this, function(_a) {
            gap = 16;
            size = this.rootElement.getBoundingClientRect().width - 120;
            this.firstLevelLiElements.forEach(function(liElement) {
              var itemSize = liElement.getBoundingClientRect().width + gap;
              if (size - itemSize >= 0) {
                size -= itemSize;
              } else {
                size -= itemSize;
                _this.createDeferredContainer();
                _this.deferredItemsContainer.removeAttribute("hidden");
                _this.deferredItemsContainer.setAttribute("aria-hidden", "false");
                _this.temporaryListForDeferredItems.appendChild(liElement);
              }
            });
            return [
              2
              /*return*/
            ];
          });
        });
      };
      Object.defineProperty(MainNavigation2.prototype, "temporaryListForDeferredItems", {
        get: function() {
          return this.deferredItemsContainer.querySelector("ul");
        },
        enumerable: false,
        configurable: true
      });
      MainNavigation2.prototype.createDeferredContainer = function() {
        var _this = this;
        if (this.deferredItemsContainer) {
          return;
        }
        var controlId = createID("MenuDeferred");
        var ul = getDocument().createElement("ul");
        var li = getDocument().createElement("li");
        var trigger = getDocument().createElement("gov-button");
        var icon = getDocument().createElement("gov-icon");
        var deferredName = this.rootElement.getAttribute("data-deferred-item-name");
        li.classList.add("js-deferred-items-container");
        trigger.setAttribute("type", "base");
        trigger.setAttribute("color", "primary");
        trigger.setAttribute("size", "l");
        trigger.setAttribute("aria-expanded", "false");
        trigger.setAttribute("aria-controls", controlId);
        trigger.setAttribute("aria-label", "Zobrazit dal\u0161\xED polo\u017Eky");
        trigger.innerHTML = deferredName !== null && deferredName !== void 0 ? deferredName : "Dal\u0161\xED";
        icon.setAttribute("type", "templates");
        icon.setAttribute("name", "chevron-down");
        icon.setAttribute("size", "l");
        icon.setAttribute("slot", "icon-end");
        ul.setAttribute("id", controlId);
        ul.setAttribute("hidden", "hidden");
        ul.setAttribute("aria-hidden", "true");
        ul.classList.add("gov-deferred-navigation");
        trigger.appendChild(icon);
        trigger.addEventListener("gov-click", blockRapidClicks(function() {
          return _this.toggleMenu(trigger);
        }));
        li.appendChild(trigger);
        li.appendChild(ul);
        this.rootUlElement.appendChild(li);
      };
      MainNavigation2.prototype.resetDeferredMenu = function() {
        var _this = this;
        var _a, _b, _c;
        (_a = this.deferredItemsContainer) === null || _a === void 0 ? void 0 : _a.querySelectorAll(":scope > ul > li").forEach(function(liElement) {
          return _this.rootUlElement.appendChild(liElement);
        });
        (_b = this.rootUlElement) === null || _b === void 0 ? void 0 : _b.querySelectorAll(":scope > .gov-mobile-only").forEach(function(liElement) {
          return _this.rootUlElement.appendChild(liElement);
        });
        (_c = this.deferredItemsContainer) === null || _c === void 0 ? void 0 : _c.remove();
      };
      MainNavigation2.prototype.calculateSizeOfElements = function() {
        this.firstLevelLiElements.forEach(function(liElement, i) {
          liElement.setAttribute("data-item-width", liElement.getBoundingClientRect().width.toString());
          liElement.setAttribute("data-item-index", i.toString());
        });
      };
      MainNavigation2.prototype.registerClickTriggers = function() {
        var _this = this;
        this.firstLevelLiElements.forEach(function(liElement) {
          var trigger = _this.getTriggerInLiElement(liElement);
          var innerList = _this.getInnerListInLiElement(liElement);
          if (!trigger || !innerList) {
            return;
          }
          trigger.addEventListener("gov-click", blockRapidClicks(function() {
            return _this.toggleMenu(trigger);
          }));
          trigger.addEventListener("click", blockRapidClicks(function() {
            return _this.toggleMenu(trigger);
          }));
        });
      };
      MainNavigation2.prototype.hideOpenedMenu = function(icons) {
        var _this = this;
        this.firstLevelLiElements.forEach(function(liElement) {
          var trigger = _this.getTriggerInLiElement(liElement);
          var innerList = _this.getInnerListInLiElement(liElement);
          if (!trigger || !innerList) {
            return;
          }
          var controlId = _this.resolveTriggerType(trigger).getAttribute("aria-controls");
          var isExpanded = toBool(_this.resolveTriggerType(trigger).getAttribute("aria-expanded"));
          var menuLevel = getDocument().querySelector('[id="' + controlId + '"]');
          var iconElement = trigger.querySelector("gov-icon");
          if (isExpanded) {
            trigger.setAttribute("aria-expanded", "false");
            menuLevel.setAttribute("hidden", "hidden");
            menuLevel.setAttribute("aria-hidden", "true");
            iconElement && iconElement.setAttribute("name", icons[0]);
          }
        });
      };
      MainNavigation2.prototype.toggleMenu = function(trigger, icons) {
        if (icons === void 0) {
          icons = ["chevron-down", "chevron-up"];
        }
        this.hideOpenedMenu(icons);
        var controlId = this.resolveTriggerType(trigger).getAttribute("aria-controls");
        var isExpanded = toBool(this.resolveTriggerType(trigger).getAttribute("aria-expanded"));
        var menuLevel = getDocument().querySelector('[id="' + controlId + '"]');
        var iconElement = trigger.querySelector("gov-icon");
        if (!menuLevel) {
          return;
        }
        if (isExpanded) {
          trigger.setAttribute("aria-expanded", "false");
          menuLevel.setAttribute("hidden", "hidden");
          menuLevel.setAttribute("aria-hidden", "true");
          iconElement && iconElement.setAttribute("name", icons[0]);
        } else {
          trigger.setAttribute("aria-expanded", "true");
          menuLevel.removeAttribute("hidden");
          menuLevel.setAttribute("aria-hidden", "false");
          iconElement && iconElement.setAttribute("name", icons[1]);
        }
        var elementToFocus = firstFocusableElement(menuLevel);
        if (elementToFocus) {
          elementToFocus.focus && elementToFocus.focus();
        }
      };
      MainNavigation2.prototype.verifyAndFixAccessibility = function(ul) {
        var _this = this;
        this.getLiElementsInUlElement(ul).forEach(function(liElement) {
          var trigger = _this.getTriggerInLiElement(liElement);
          var innerList = _this.getInnerListInLiElement(liElement);
          if (!trigger || !innerList) {
            return;
          }
          var controlsId = createID("MainMenu");
          if (trigger.getAttribute("aria-controls") !== innerList.getAttribute("id")) {
            trigger.setAttribute("aria-controls", controlsId);
            innerList.setAttribute("id", controlsId);
          }
          if (!trigger.getAttribute("aria-expanded")) {
            trigger.setAttribute("aria-expanded", "false");
          }
          if (!trigger.getAttribute("aria-label")) {
            trigger.setAttribute("aria-label", "Zobrazit / Skr\xFDt polo\u017Eky sekce " + trigger.textContent);
          }
          _this.verifyAndFixAccessibility(innerList);
        });
      };
      MainNavigation2.prototype.accessibilityMobileNavigation = function() {
        return __awaiter(this, void 0, void 0, function() {
          var trigger, menu, controller, controlsId;
          var _this = this;
          return __generator(this, function(_a) {
            switch (_a.label) {
              case 0:
                trigger = getDocument().querySelector(".js-gov-header__navigation-trigger");
                menu = this.headerNavigationElement;
                if (!trigger || !menu) {
                  return [
                    2
                    /*return*/
                  ];
                }
                return [4, delay(500)];
              case 1:
                _a.sent();
                controller = this.resolveTriggerType(trigger);
                controlsId = createID("MainMobileMenu");
                if (controller.getAttribute("aria-controls") !== menu.getAttribute("id") || menu.getAttribute("id") === null) {
                  trigger.setAttribute("aria-controls", controlsId);
                  menu.setAttribute("id", controlsId);
                }
                if (!controller.getAttribute("aria-expanded")) {
                  trigger.setAttribute("aria-expanded", "false");
                }
                if (!controller.getAttribute("aria-label")) {
                  trigger.setAttribute("aria-label", "Zobrazit / Skr\xFDt menu");
                }
                trigger.addEventListener("gov-click", blockRapidClicks(function() {
                  return _this.toggleMenu(trigger, ["list", "x-lg"]);
                }));
                trigger.addEventListener("click", blockRapidClicks(function() {
                  return _this.toggleMenu(trigger, ["list", "x-lg"]);
                }));
                return [
                  2
                  /*return*/
                ];
            }
          });
        });
      };
      MainNavigation2.prototype.controlHeaderNavigation = function(show) {
        var _a, _b, _c, _d;
        if (show === void 0) {
          show = true;
        }
        if (show) {
          (_a = this.headerNavigationElement) === null || _a === void 0 ? void 0 : _a.removeAttribute("hidden");
          (_b = this.headerNavigationElement) === null || _b === void 0 ? void 0 : _b.removeAttribute("aria-hidden");
        } else {
          (_c = this.headerNavigationElement) === null || _c === void 0 ? void 0 : _c.setAttribute("hidden", "hidden");
          (_d = this.headerNavigationElement) === null || _d === void 0 ? void 0 : _d.setAttribute("aria-hidden", "true");
        }
      };
      MainNavigation2.prototype.resolveTriggerType = function(trigger) {
        return trigger && trigger.nodeName === "GOV-BUTTON" ? trigger.querySelector("button") : trigger;
      };
      MainNavigation2.prototype.getTriggerInLiElement = function(liElement) {
        return liElement.querySelector(":scope > gov-button, :scope > button");
      };
      MainNavigation2.prototype.getInnerListInLiElement = function(liElement) {
        return liElement.querySelector(":scope > ul");
      };
      MainNavigation2.prototype.getLiElementsInUlElement = function(ulElement) {
        return ulElement.querySelectorAll(":scope > li");
      };
      Object.defineProperty(MainNavigation2.prototype, "firstLevelLiElements", {
        get: function() {
          return this.rootUlElement.querySelectorAll(":scope > li:not(.gov-mobile-only)");
        },
        enumerable: false,
        configurable: true
      });
      Object.defineProperty(MainNavigation2.prototype, "deferredItemsContainer", {
        get: function() {
          return this.rootElement.querySelector(".js-deferred-items-container");
        },
        enumerable: false,
        configurable: true
      });
      Object.defineProperty(MainNavigation2.prototype, "rootUlElement", {
        get: function() {
          return this.rootElement.querySelector(":scope > ul");
        },
        enumerable: false,
        configurable: true
      });
      Object.defineProperty(MainNavigation2.prototype, "headerNavigationElement", {
        get: function() {
          return getDocument().querySelector(".js-gov-header__navigation");
        },
        enumerable: false,
        configurable: true
      });
      return MainNavigation2;
    })()
  );
  var main_navigation_default = MainNavigation;

  // node_modules/@gov-design-system-ce/templates/dist/scripts/footer-button-up.js
  var FooterButtonUp = (
    /** @class */
    (function() {
      function FooterButtonUp2(rootElement) {
        this.rootElement = rootElement;
        this.registerListeners();
      }
      FooterButtonUp2.prototype.registerListeners = function() {
        var _this = this;
        this.rootElement.addEventListener("gov-click", blockRapidClicks(function() {
          return _this.scrollToTop();
        }));
        this.rootElement.addEventListener("click", blockRapidClicks(function() {
          return _this.scrollToTop();
        }));
      };
      FooterButtonUp2.prototype.scrollToTop = function() {
        smoothScrollTo(0);
      };
      return FooterButtonUp2;
    })()
  );
  var footer_button_up_default = FooterButtonUp;

  // node_modules/@gov-design-system-ce/templates/dist/scripts/scripts.js
  var initTemplateScripts = function() {
    getDocument().querySelectorAll(".gov-navigation").forEach(function(el) {
      new main_navigation_default(el);
    });
    getDocument().querySelectorAll(".js-gov-footer__up").forEach(function(el) {
      new footer_button_up_default(el);
    });
  };
  getWindow().initTemplateScripts = initTemplateScripts;
})();
